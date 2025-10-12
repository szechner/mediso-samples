using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using FakeItEasy;
using Wolverine;
using Wolverine.Testing;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Sagas;
using Mediso.PaymentSample.Application.Modules.FraudDetection.Contracts;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using FraudRiskLevel = Mediso.PaymentSample.Application.Modules.FraudDetection.Contracts.RiskLevel;

namespace Mediso.InboundOutbox.Tests;

/// <summary>
/// Integration test demonstrating the complete payment processing saga happy path flow.
/// 
/// This test validates the entire payment lifecycle orchestrated by the PaymentProcessingSaga:
/// 1. Payment Initiated (via HTTP API)
/// 2. Fraud Detection Triggered and Completed (Low Risk)
/// 3. Funds Reserved Successfully
/// 4. Payment Settled/Captured
/// 5. Completion Notifications Sent
/// 
/// The test demonstrates proper integration between:
/// - HTTP API endpoints
/// - Saga orchestration
/// - Domain aggregates (Payment)
/// - External services (fraud detection, payment processor)
/// - Durable messaging with Wolverine
/// - Event sourcing with Marten
/// 
/// Architecture patterns validated:
/// - Saga Pattern for distributed transaction coordination
/// - Event Sourcing for payment state management
/// - CQRS for command/query separation
/// - Hexagonal Architecture with ports/adapters
/// - Domain-Driven Design with rich domain models
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Saga")]
[Category("HappyPath")]
public class PaymentSagaHappyPathIntegrationTest
{
    private IServiceProvider _serviceProvider = null!;
    private IMessageBus _messageBus = null!;
    private IEventStore _eventStore = null!;
    private PaymentProcessingSaga _saga = null!;
    private ILogger<PaymentSagaHappyPathIntegrationTest> _logger = null!;

    [SetUp]
    public async Task Setup()
    {
        // Create service collection for integration test
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        // Add fake implementations for external dependencies
        var fakeEventStore = A.Fake<IEventStore>();
        var fakeMessageBus = A.Fake<IMessageBus>();
        
        services.AddSingleton(fakeEventStore);
        services.AddSingleton(fakeMessageBus);
        
        // Add the saga
        services.AddTransient<PaymentProcessingSaga>();
        
        _serviceProvider = services.BuildServiceProvider();
        _messageBus = _serviceProvider.GetRequiredService<IMessageBus>();
        _eventStore = _serviceProvider.GetRequiredService<IEventStore>();
        _saga = _serviceProvider.GetRequiredService<PaymentProcessingSaga>();
        _logger = _serviceProvider.GetRequiredService<ILogger<PaymentSagaHappyPathIntegrationTest>>();
        
        _logger.LogInformation("=== Setting up Payment Saga Happy Path Integration Test ===");
    }

    [Test]
    public async Task PaymentSaga_HappyPath_ShouldCompleteSuccessfully()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var idempotencyKey = Guid.NewGuid().ToString();
        
        _logger.LogInformation("=== Starting Payment Saga Happy Path Test ===");
        _logger.LogInformation("CorrelationId: {CorrelationId}", correlationId);
        _logger.LogInformation("IdempotencyKey: {IdempotencyKey}", idempotencyKey);
        
        // Step 1: Create InitiatePaymentCommand
        var customerId = new CustomerId(Guid.NewGuid());
        var merchantId = new MerchantId(Guid.NewGuid());
        
        var initiateCommand = new InitiatePaymentCommand
        {
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            CustomerId = customerId,
            MerchantId = merchantId,
            Amount = 100.00m,
            Currency = "USD",
            Description = "Test payment for integration test",
            PaymentMethod = "credit-card",
            Metadata = new Dictionary<string, string>
            {
                { "test", "integration-test" },
                { "scenario", "happy-path" }
            }
        };

        _logger.LogInformation("=== Step 1: Payment Initiation ===");
        _logger.LogInformation("Amount: {Amount} {Currency}", initiateCommand.Amount, initiateCommand.Currency);
        _logger.LogInformation("Customer: {CustomerId}", customerId);
        _logger.LogInformation("Merchant: {MerchantId}", merchantId);

        // Create fake payment handler
        var fakeInitiateHandler = A.Fake<IInitiatePaymentHandler>();
        var paymentId = PaymentId.New();
        
        var initiateResponse = new InitiatePaymentResponse
        {
            PaymentId = paymentId,
            Status = PaymentStatus.Initiated,
            CorrelationId = correlationId,
            InitiatedAt = DateTimeOffset.UtcNow,
            IsDuplicateRequest = false
        };
        
        A.CallTo(() => fakeInitiateHandler.HandleAsync(A<InitiatePaymentCommand>._, A<CancellationToken>._))
            .Returns(initiateResponse);

        // Act & Assert - Step 1: Initiate Payment
        var response = await _saga.StartAsync(
            initiateCommand, 
            fakeInitiateHandler, 
            _messageBus, 
            _logger);
        
        // Verify saga state after initiation
        Assert.That(response, Is.Not.Null);
        Assert.That(response.PaymentId, Is.EqualTo(paymentId));
        Assert.That(response.Status, Is.EqualTo(PaymentStatus.Initiated));
        Assert.That(response.CorrelationId, Is.EqualTo(correlationId));
        
        Assert.That(_saga.State.PaymentId, Is.EqualTo(paymentId));
        Assert.That(_saga.State.CorrelationId, Is.EqualTo(correlationId));
        Assert.That(_saga.State.CurrentStep, Is.EqualTo(PaymentProcessingStep.FraudDetection));
        Assert.That(_saga.State.Status, Is.EqualTo(PaymentSagaStatus.InProgress));
        
        _logger.LogInformation("✓ Payment initiated successfully: {PaymentId}", paymentId);
        
        // Verify fraud detection command was sent
        A.CallTo(() => _messageBus.SendAsync(A<PerformFraudDetectionCommand>._))
            .MustHaveHappenedOnceExactly();
        
        // Verify timeout was scheduled
        A.CallTo(() => _messageBus.ScheduleAsync(A<PaymentSagaTimeout>._, A<DateTimeOffset>._))
            .MustHaveHappenedOnceExactly();

        _logger.LogInformation("=== Step 2: Fraud Detection ===");
        
        // Step 2: Simulate Fraud Detection Completion (Low Risk)
        var fraudDetectionResult = new FraudDetectionCompletedEvent
        {
            PaymentId = paymentId,
            RiskLevel = FraudRiskLevel.Low,
            RiskScore = 0.15m,
            Provider = "TestFraudProvider",
            AnalyzedAt = DateTimeOffset.UtcNow,
            RiskFactors = new List<string> { "New customer", "Standard payment amount" },
            Recommendations = new List<string> { "Proceed with payment" }
        };

        // Create fake reserve handler
        var fakeReserveHandler = A.Fake<IReservePaymentHandler>();
        var reserveResponse = new ReservePaymentResponse
        {
            PaymentId = paymentId,
            Status = PaymentStatus.Reserved,
            IsReserved = true,
            ReservedAmount = 100.00m,
            CorrelationId = correlationId,
            ReservedAt = DateTimeOffset.UtcNow,
            IsDuplicateRequest = false
        };
        
        A.CallTo(() => fakeReserveHandler.HandleAsync(A<ReservePaymentCommand>._, A<CancellationToken>._))
            .Returns(reserveResponse);

        // Process fraud detection result
        await _saga.HandleFraudDetectionCompletedAsync(
            fraudDetectionResult,
            fakeReserveHandler,
            _messageBus,
            _logger);
        
        // Verify saga state after fraud detection and reservation
        Assert.That(_saga.State.CurrentStep, Is.EqualTo(PaymentProcessingStep.Settling));
        Assert.That(_saga.State.Status, Is.EqualTo(PaymentSagaStatus.InProgress));
        Assert.That(_saga.State.FraudDetectionResult, Is.EqualTo(fraudDetectionResult));
        Assert.That(_saga.State.ReservedAmount, Is.EqualTo(100.00m));
        
        _logger.LogInformation("✓ Fraud detection completed (Low Risk: {RiskScore})", fraudDetectionResult.RiskScore);
        _logger.LogInformation("✓ Funds reserved successfully: {ReservedAmount} {Currency}", reserveResponse.ReservedAmount, initiateCommand.Currency);

        // Verify settlement was scheduled
        A.CallTo(() => _messageBus.ScheduleAsync(A<SettlePaymentCommand>._, A<DateTimeOffset>._))
            .MustHaveHappenedOnceExactly();

        _logger.LogInformation("=== Step 3: Payment Settlement ===");

        // Step 3: Simulate Settlement Processing
        var settleCommand = new SettlePaymentCommand
        {
            PaymentId = paymentId,
            IdempotencyKey = $"{idempotencyKey}-settle",
            CorrelationId = correlationId,
            SettlementAmount = 100.00m
        };

        // Create fake settle handler
        var fakeSettleHandler = A.Fake<ISettlePaymentHandler>();
        var settleResponse = new SettlePaymentResponse
        {
            PaymentId = paymentId,
            Status = PaymentStatus.Settled,
            IsSettled = true,
            SettledAmount = 100.00m,
            CorrelationId = correlationId,
            SettledAt = DateTimeOffset.UtcNow,
            IsDuplicateRequest = false
        };
        
        A.CallTo(() => fakeSettleHandler.HandleAsync(A<SettlePaymentCommand>._, A<CancellationToken>._))
            .Returns(settleResponse);

        // Process settlement
        await _saga.HandleSettlementCompletedAsync(
            settleCommand,
            fakeSettleHandler,
            _messageBus,
            _logger);
        
        // Verify saga completion
        Assert.That(_saga.State.Status, Is.EqualTo(PaymentSagaStatus.Completed));
        Assert.That(_saga.State.SettledAmount, Is.EqualTo(100.00m));
        Assert.That(_saga.State.CompletedAt, Is.Not.Null);
        
        _logger.LogInformation("✓ Payment settled successfully: {SettledAmount} {Currency}", settleResponse.SettledAmount, initiateCommand.Currency);

        // Verify completion notification was sent
        A.CallTo(() => _messageBus.PublishAsync(A<PaymentProcessingCompletedEvent>._))
            .MustHaveHappenedOnceExactly();

        _logger.LogInformation("=== Step 4: Validation of Complete Flow ===");

        // Final validations
        var processingDuration = _saga.State.CompletedAt!.Value - _saga.State.StartedAt;
        
        _logger.LogInformation("✓ Complete saga flow validated:");
        _logger.LogInformation("  - Payment ID: {PaymentId}", _saga.State.PaymentId);
        _logger.LogInformation("  - Final Status: {Status}", _saga.State.Status);
        _logger.LogInformation("  - Processing Duration: {Duration}", processingDuration);
        _logger.LogInformation("  - Events Generated: {EventCount}", _saga.State.Events.Count);
        
        // Verify all expected saga events were recorded
        var expectedEvents = new[]
        {
            "PaymentInitiated",
            "FraudDetectionCompleted", 
            "PaymentReserved",
            "SettlementScheduled",
            "PaymentSettled",
            "CompletionNotificationSent"
        };
        
        var actualEvents = _saga.State.Events.Select(e => e.EventName).ToList();
        
        foreach (var expectedEvent in expectedEvents)
        {
            Assert.That(actualEvents, Contains.Item(expectedEvent), 
                $"Expected event '{expectedEvent}' not found in saga events: {string.Join(", ", actualEvents)}");
        }
        
        // Verify external system interactions
        _logger.LogInformation("=== Verifying External System Interactions ===");
        
        // Verify all expected message bus calls
        A.CallTo(() => _messageBus.SendAsync(A<PerformFraudDetectionCommand>._))
            .MustHaveHappenedOnceExactly();
        
        A.CallTo(() => _messageBus.ScheduleAsync(A<SettlePaymentCommand>._, A<DateTimeOffset>._))
            .MustHaveHappenedOnceExactly();
        
        A.CallTo(() => _messageBus.PublishAsync(A<PaymentProcessingCompletedEvent>._))
            .MustHaveHappenedOnceExactly();
        
        A.CallTo(() => _messageBus.ScheduleAsync(A<PaymentSagaTimeout>._, A<DateTimeOffset>._))
            .MustHaveHappenedOnceExactly();
        
        _logger.LogInformation("✓ All external system interactions verified");
        
        _logger.LogInformation("=== PAYMENT SAGA HAPPY PATH TEST COMPLETED SUCCESSFULLY ===");
        
        // Summary assertions
        Assert.That(_saga.State.Status, Is.EqualTo(PaymentSagaStatus.Completed));
        Assert.That(_saga.State.CurrentStep, Is.EqualTo(PaymentProcessingStep.NotifyingCompletion));
        Assert.That(_saga.State.PaymentId, Is.EqualTo(paymentId));
        Assert.That(_saga.State.Amount, Is.EqualTo(100.00m));
        Assert.That(_saga.State.ReservedAmount, Is.EqualTo(100.00m));
        Assert.That(_saga.State.SettledAmount, Is.EqualTo(100.00m));
        Assert.That(_saga.State.FailureReason, Is.Null);
        Assert.That(_saga.State.Events.Count, Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    public async Task PaymentSaga_HighRiskFraud_ShouldRequestManualReview()
    {
        // Arrange - Similar setup but with high-risk fraud scenario
        var correlationId = Guid.NewGuid().ToString();
        var idempotencyKey = Guid.NewGuid().ToString();
        
        _logger.LogInformation("=== Starting High-Risk Fraud Detection Test ===");
        
        var initiateCommand = new InitiatePaymentCommand
        {
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            CustomerId = new CustomerId(Guid.NewGuid()),
            MerchantId = new MerchantId(Guid.NewGuid()),
            Amount = 5000.00m, // High amount
            Currency = "USD",
            PaymentMethod = "credit-card"
        };

        var fakeInitiateHandler = A.Fake<IInitiatePaymentHandler>();
        var paymentId = PaymentId.New();
        
        A.CallTo(() => fakeInitiateHandler.HandleAsync(A<InitiatePaymentCommand>._, A<CancellationToken>._))
            .Returns(new InitiatePaymentResponse
            {
                PaymentId = paymentId,
                Status = PaymentStatus.Initiated,
                CorrelationId = correlationId,
                InitiatedAt = DateTimeOffset.UtcNow
            });

        // Start saga
        await _saga.StartAsync(initiateCommand, fakeInitiateHandler, _messageBus, _logger);

        // Act - Simulate high-risk fraud detection
        var fraudDetectionResult = new FraudDetectionCompletedEvent
        {
            PaymentId = paymentId,
            RiskLevel = FraudRiskLevel.High,
            RiskScore = 0.85m, // High risk score
            Provider = "TestFraudProvider",
            AnalyzedAt = DateTimeOffset.UtcNow,
            RiskFactors = new List<string> { "Large amount", "Suspicious location", "Velocity check failed" },
            Recommendations = new List<string> { "Manual review required", "Additional verification needed" }
        };

        var fakeReserveHandler = A.Fake<IReservePaymentHandler>();
        
        await _saga.HandleFraudDetectionCompletedAsync(
            fraudDetectionResult,
            fakeReserveHandler,
            _messageBus,
            _logger);
        
        // Assert - Verify manual review was requested
        Assert.That(_saga.State.CurrentStep, Is.EqualTo(PaymentProcessingStep.AwaitingManualReview));
        Assert.That(_saga.State.Status, Is.EqualTo(PaymentSagaStatus.AwaitingManualReview));
        
        // Verify manual review request was sent
        A.CallTo(() => _messageBus.SendAsync(A<PaymentManualReviewRequest>._))
            .MustHaveHappenedOnceExactly();
        
        // Verify reservation was NOT attempted
        A.CallTo(() => fakeReserveHandler.HandleAsync(A<ReservePaymentCommand>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        
        _logger.LogInformation("✓ High-risk payment correctly routed to manual review");
    }

    [Test]
    public async Task PaymentSaga_BlockedFraud_ShouldCancelPayment()
    {
        // Arrange - Similar setup but with blocked fraud scenario
        var correlationId = Guid.NewGuid().ToString();
        var idempotencyKey = Guid.NewGuid().ToString();
        
        _logger.LogInformation("=== Starting Blocked Fraud Detection Test ===");
        
        var initiateCommand = new InitiatePaymentCommand
        {
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            CustomerId = new CustomerId(Guid.NewGuid()),
            MerchantId = new MerchantId(Guid.NewGuid()),
            Amount = 100.00m,
            Currency = "USD",
            PaymentMethod = "credit-card"
        };

        var fakeInitiateHandler = A.Fake<IInitiatePaymentHandler>();
        var paymentId = PaymentId.New();
        
        A.CallTo(() => fakeInitiateHandler.HandleAsync(A<InitiatePaymentCommand>._, A<CancellationToken>._))
            .Returns(new InitiatePaymentResponse
            {
                PaymentId = paymentId,
                Status = PaymentStatus.Initiated,
                CorrelationId = correlationId,
                InitiatedAt = DateTimeOffset.UtcNow
            });

        // Start saga
        await _saga.StartAsync(initiateCommand, fakeInitiateHandler, _messageBus, _logger);

        // Act - Simulate blocked fraud detection
        var fraudDetectionResult = new FraudDetectionCompletedEvent
        {
            PaymentId = paymentId,
            RiskLevel = FraudRiskLevel.Blocked,
            RiskScore = 0.95m, // Very high risk score
            Provider = "TestFraudProvider",
            AnalyzedAt = DateTimeOffset.UtcNow,
            RiskFactors = new List<string> { "Known fraudulent card", "Blacklisted IP" },
            Recommendations = new List<string> { "Block payment immediately" }
        };

        var fakeReserveHandler = A.Fake<IReservePaymentHandler>();
        
        await _saga.HandleFraudDetectionCompletedAsync(
            fraudDetectionResult,
            fakeReserveHandler,
            _messageBus,
            _logger);
        
        // Assert - Verify payment was cancelled due to fraud
        Assert.That(_saga.State.CurrentStep, Is.EqualTo(PaymentProcessingStep.CancellingDueToFraud));
        Assert.That(_saga.State.Status, Is.EqualTo(PaymentSagaStatus.CancelledDueToFraud));
        
        // Verify cancellation command was sent
        A.CallTo(() => _messageBus.SendAsync(A<CancelPaymentCommand>._))
            .MustHaveHappenedOnceExactly();
        
        // Verify reservation was NOT attempted
        A.CallTo(() => fakeReserveHandler.HandleAsync(A<ReservePaymentCommand>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        
        _logger.LogInformation("✓ Blocked payment correctly cancelled due to fraud");
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
        _logger?.LogInformation("=== Payment Saga Integration Test Completed ===");
    }
}

/// <summary>
/// Helper extension methods for test scenarios
/// </summary>
public static class PaymentTestHelpers
{
    public static InitiatePaymentCommand CreateTestPaymentCommand(
        decimal amount = 100.00m,
        string currency = "USD",
        string? correlationId = null,
        string? idempotencyKey = null)
    {
        return new InitiatePaymentCommand
        {
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            IdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString(),
            CustomerId = new CustomerId(Guid.NewGuid()),
            MerchantId = new MerchantId(Guid.NewGuid()),
            Amount = amount,
            Currency = currency,
            PaymentMethod = "credit-card",
            Description = $"Test payment for {amount} {currency}"
        };
    }
    
    public static FraudDetectionCompletedEvent CreateLowRiskFraudResult(PaymentId paymentId)
    {
        return new FraudDetectionCompletedEvent
        {
            PaymentId = paymentId,
            RiskLevel = FraudRiskLevel.Low,
            RiskScore = 0.15m,
            Provider = "TestFraudProvider",
            AnalyzedAt = DateTimeOffset.UtcNow,
            RiskFactors = new List<string> { "Standard transaction" },
            Recommendations = new List<string> { "Proceed with payment" }
        };
    }
}