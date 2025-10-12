using System.Diagnostics;
using FluentValidation;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Ports.Primary;
using Mediso.PaymentSample.Application.Modules.FraudDetection.Contracts;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;
using FraudRiskLevel = Mediso.PaymentSample.Application.Modules.FraudDetection.Contracts.RiskLevel;

namespace Mediso.PaymentSample.Application.Modules.Payments.Sagas;

/// <summary>
/// Payment Processing Saga orchestrates the complete payment lifecycle with compensation patterns.
/// 
/// Saga Pattern Implementation:
/// - Orchestrates complex business processes across multiple bounded contexts
/// - Implements compensation actions for failure scenarios
/// - Maintains saga state and manages timeouts
/// - Provides comprehensive observability and audit trails
/// - Handles partial failures with graceful degradation
/// 
/// Payment Flow:
/// 1. Payment Initiated → Fraud Detection
/// 2. If Low Risk → Reserve Funds
/// 3. If Reserved → Settle Payment
/// 4. Send Notifications
/// 5. Handle Failures with Compensation
/// </summary>
public class PaymentProcessingSaga : Saga
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Sagas");

    public Guid Id { get; set; }

    /// <summary>
    /// Saga state containing payment processing information and state tracking.
    /// </summary>
    public PaymentProcessingSagaState State { get; set; }

    /// <summary>
    /// Starts the payment processing saga when a payment is initiated.
    /// </summary>
    public static async Task<PaymentProcessingSaga> Start(
        InitiatePaymentCommand command,
        IInitiatePaymentHandler paymentHandler,
        IMessageBus messageBus,
        IQuerySession query,
        ILogger<PaymentProcessingSaga> logger,
        IValidator<InitiatePaymentCommand> InitiatePaymentCommandvalidator,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("PaymentSaga.Start");
        activity?.SetTag(TracingConstants.CorrelationId, command.CorrelationId);
        activity?.SetTag(TracingConstants.IdempotencyKey, command.IdempotencyKey);
        activity?.SetTag("payment.amount", command.Amount);
        activity?.SetTag("payment.currency", command.Currency);

        logger.LogInformation(
            "Starting Payment Processing Saga for {Amount} {Currency} [CorrelationId: {CorrelationId}]",
            command.Amount, command.Currency, command.CorrelationId);

        await ValidateInitiatePaymentCommandAsync(command, InitiatePaymentCommandvalidator);
        
        try
        {
            var saga = new PaymentProcessingSaga
            {
                Id = command.PaymentProcessingSagaId,
                State = new PaymentProcessingSagaState
                {
                    Id = command.PaymentProcessingSagaId,
                    CorrelationId = command.CorrelationId,
                    IdempotencyKey = command.IdempotencyKey,
                    CustomerId = command.CustomerId,
                    MerchantId = command.MerchantId,
                    Amount = command.Amount,
                    Currency = command.Currency,
                    PaymentMethod = command.PaymentMethod,
                    StartedAt = DateTimeOffset.UtcNow,
                    CurrentStep = PaymentProcessingStep.Initiating,
                    Status = PaymentSagaStatus.InProgress
                }
            };
            activity?.SetTag("saga.id", command.PaymentProcessingSagaId);
            activity?.SetTag("saga.step", PaymentProcessingStep.Initiating);
            activity?.SetTag("saga.status", PaymentSagaStatus.InProgress);
            var response = await paymentHandler.HandleAsync(command, cancellationToken);
            saga.State.PaymentId = response.PaymentId;
            saga.State.CurrentStep = PaymentProcessingStep.FraudDetection;
            saga.State.Events.Add(new SagaEvent("PaymentInitiated", response));
            
            await messageBus.SendAsync(new PerformFraudDetectionCommand
            {
                PaymentProcessingSagaId = command.PaymentProcessingSagaId,
                PaymentId = response.PaymentId,
                CustomerId = command.CustomerId,
                Amount = command.Amount,
                Currency = command.Currency,
                PaymentMethod = command.PaymentMethod,
                CorrelationId = command.CorrelationId,
                Metadata = command.Metadata ?? new()
            });
            
            await messageBus.ScheduleAsync(
                new PaymentSagaTimeout { PaymentProcessingSagaId = command.PaymentProcessingSagaId, PaymentId = response.PaymentId},
                DateTimeOffset.UtcNow.AddMinutes(10));

            logger.LogInformation(
                "Payment Processing Saga started, PaymentId: {PaymentId} [CorrelationId: {CorrelationId}]",
                response.PaymentId, command.CorrelationId);

            activity?.SetTag("payment.id", response.PaymentId.Value);

            return saga;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to start Payment Processing Saga [CorrelationId: {CorrelationId}]",
                command.CorrelationId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Handles fraud detection completion and proceeds with payment reservation.
    /// </summary>
    public async Task Handle(
        FraudDetectionCompletedEvent fraudResult,
        IReservePaymentHandler reserveHandler,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("PaymentSaga.FraudDetectionCompleted");
        activity?.SetTag(TracingConstants.CorrelationId, State.CorrelationId);
        activity?.SetTag("saga.id", Id);
        activity?.SetTag("payment.id", fraudResult.PaymentId.Value);
        activity?.SetTag("fraud.risk_level", fraudResult.RiskLevel.ToString());
        activity?.SetTag("fraud.score", fraudResult.RiskScore);

        logger.LogInformation(
            "Processing fraud detection result for payment {PaymentId}, Risk: {RiskLevel}, Score: {RiskScore} [CorrelationId: {CorrelationId}]",
            fraudResult.PaymentId, fraudResult.RiskLevel, fraudResult.RiskScore, State.CorrelationId);

        try
        {
            State.CurrentStep = PaymentProcessingStep.ProcessingFraudResult;
            State.FraudDetectionResult = fraudResult;
            State.Events.Add(new SagaEvent("FraudDetectionCompleted", fraudResult));

            // Decision: Proceed based on risk level
            if (fraudResult.RiskLevel == FraudRiskLevel.Blocked)
            {
                // Cancel payment due to high fraud risk
                await CancelPaymentDueToFraudAsync(fraudResult.PaymentId, messageBus, logger, cancellationToken);
                return;
            }

            if (fraudResult.RiskLevel == FraudRiskLevel.High)
            {
                // Require manual review for high-risk payments
                await RequestManualReviewAsync(fraudResult.PaymentId, messageBus, logger, cancellationToken);
                return;
            }

            // Proceed with reservation for Low and Medium risk
            State.CurrentStep = PaymentProcessingStep.Reserving;

            var reserveCommand = new ReservePaymentCommand
            {
                PaymentProcessingSagaId = Id,
                PaymentId = fraudResult.PaymentId,
                IdempotencyKey = $"{State.IdempotencyKey}-reserve",
                CorrelationId = State.CorrelationId,
                RiskScore = fraudResult.RiskScore,
                FraudDetection = new FraudDetectionResult
                {
                    RiskLevel = MapFraudRiskLevelToContractsRiskLevel(fraudResult.RiskLevel),
                    Score = fraudResult.RiskScore,
                    RiskFactors = fraudResult.RiskFactors ?? new List<string>(),
                    Recommendations = fraudResult.Recommendations ?? new List<string>(),
                    Provider = fraudResult.Provider,
                    AnalyzedAt = fraudResult.AnalyzedAt
                }
            };

            var response = await reserveHandler.HandleAsync(reserveCommand, cancellationToken);

            State.Events.Add(new SagaEvent("PaymentReserved", response));

            if (response.IsReserved)
            {
                State.CurrentStep = PaymentProcessingStep.Settling;
                State.ReservedAmount = response.ReservedAmount;

                // Schedule settlement
                await ScheduleSettlementAsync(response.PaymentId, messageBus, logger, cancellationToken);
            }
            else
            {
                // Handle reservation failure
                await HandleReservationFailureAsync(response, messageBus, logger, cancellationToken);
            }

            logger.LogInformation(
                "Fraud detection processed for payment {PaymentId}, proceeding to: {CurrentStep} [CorrelationId: {CorrelationId}]",
                response.PaymentId, State.CurrentStep, State.CorrelationId);

            activity?.SetTag("saga.step", State.CurrentStep.ToString());
            activity?.SetTag("payment.is_reserved", response.IsReserved);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process fraud detection result for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                fraudResult.PaymentId, State.CorrelationId);

            await HandleSagaFailureAsync(fraudResult.PaymentId, ex, messageBus, logger, cancellationToken);
        }
    }

    /// <summary>
    /// Handles payment settlement completion.
    /// </summary>
    public async Task Handle(
        SettlePaymentCommand settleCommand,
        ISettlePaymentHandler settleHandler,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("PaymentSaga.SettlementCompleted");
        activity?.SetTag("saga.id", State.CorrelationId);
        activity?.SetTag("payment.id", settleCommand.PaymentId.Value);
        activity?.SetTag("payment.settlement_amount", settleCommand.SettlementAmount);

        logger.LogInformation(
            "Processing settlement for payment {PaymentId} with amount {SettlementAmount} [CorrelationId: {CorrelationId}]",
            settleCommand.PaymentId, settleCommand.SettlementAmount, State.CorrelationId);

        try
        {
            State.CurrentStep = PaymentProcessingStep.Settling;

            var response = await settleHandler.HandleAsync(settleCommand, cancellationToken);

            State.Events.Add(new SagaEvent("PaymentSettled", response));

            if (response.IsSettled)
            {
                State.CurrentStep = PaymentProcessingStep.NotifyingCompletion;
                State.SettledAmount = response.SettledAmount;
                State.Status = PaymentSagaStatus.Completed;
                State.CompletedAt = DateTimeOffset.UtcNow;

                // Send completion notifications
                await SendCompletionNotificationsAsync(settleCommand.PaymentId, messageBus, logger, cancellationToken);

                // Mark saga as completed
                await CompleteSagaAsync(settleCommand.PaymentId, logger);
            }
            else
            {
                // Handle settlement failure
                await HandleSettlementFailureAsync(response, messageBus, logger, cancellationToken);
            }

            activity?.SetTag("payment.is_settled", response.IsSettled);
            activity?.SetTag("saga.status", State.Status.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process settlement for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                settleCommand.PaymentId, State.CorrelationId);

            await HandleSagaFailureAsync(settleCommand.PaymentId, ex, messageBus, logger, cancellationToken);
        }
    }

    /// <summary>
    /// Handles saga timeout scenarios with appropriate compensation.
    /// </summary>
    public async Task Handle(
        PaymentSagaTimeout timeout,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("PaymentSaga.Timeout");
        activity?.SetTag("saga.id", State.CorrelationId);
        activity?.SetTag("payment.id", timeout.PaymentId.Value);
        activity?.SetTag("saga.step", State.CurrentStep.ToString());

        logger.LogWarning(
            "Payment Processing Saga timed out at step {CurrentStep} for payment {PaymentId} [CorrelationId: {CorrelationId}]",
            State.CurrentStep, timeout.PaymentId, State.CorrelationId);

        try
        {
            State.Status = PaymentSagaStatus.TimedOut;
            State.FailureReason = $"Saga timed out at step {State.CurrentStep}";
            State.Events.Add(new SagaEvent("SagaTimedOut", timeout));

            // Perform compensation based on current step
            await PerformTimeoutCompensationAsync(timeout.PaymentId, messageBus, logger, cancellationToken);

            State.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to handle saga timeout for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                timeout.PaymentId, State.CorrelationId);
        }
    }
    
    public async Task Handle(PaymentManualReviewRequest manualReviewRequest, IMessageBus messageBus, ILogger<PaymentProcessingSaga> logger, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentSaga.ManualReviewRequested");
        activity?.SetTag("saga.id", State.CorrelationId);
        activity?.SetTag("payment.id", manualReviewRequest.PaymentId.Value);
        activity?.SetTag("fraud.risk_level", manualReviewRequest.RiskLevel.ToString());
        activity?.SetTag("fraud.score", manualReviewRequest.RiskScore);

        logger.LogInformation(
            "Payment {PaymentId} requires manual review due to high fraud risk [CorrelationId: {CorrelationId}]",
            manualReviewRequest.PaymentId, State.CorrelationId);
        
        // Log the manual review request
        State.Events.Add(new SagaEvent("ManualReviewRequested", manualReviewRequest));
        State.Status = PaymentSagaStatus.AwaitingManualReview;
        State.CurrentStep = PaymentProcessingStep.AwaitingManualReview;

        State.CompletedAt = DateTimeOffset.UtcNow;
        
        // Mark saga as completed
        await CompleteSagaAsync(manualReviewRequest.PaymentId, logger);
        
        await Task.CompletedTask;
    }
    
    public static void NotFound(PaymentSagaTimeout timeout, ILogger<PaymentProcessingSaga> log)
    {
        log.LogDebug("Timeout for saga {SagaId} ignored because saga is already completed.", timeout.PaymentProcessingSagaId);
    }

    // ========================================================================================
    // PRIVATE COMPENSATION AND HELPER METHODS
    // ========================================================================================

    private static async Task ValidateInitiatePaymentCommandAsync(InitiatePaymentCommand command, IValidator<InitiatePaymentCommand> validator)
    {
        var validationResult = await validator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException($"Payment initiation validation failed: {errors}");
        }
    }
    
    private async Task CancelPaymentDueToFraudAsync(
        PaymentId paymentId,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        State.CurrentStep = PaymentProcessingStep.CancellingDueToFraud;
        State.Status = PaymentSagaStatus.CancelledDueToFraud;

        var cancelCommand = new CancelPaymentCommand
        {
            PaymentProcessingSagaId = Id,
            PaymentId = paymentId,
            IdempotencyKey = $"{State.IdempotencyKey}-cancel-fraud",
            CorrelationId = State.CorrelationId,
            CancellationReason = "Payment cancelled due to high fraud risk",
            Category = CancellationCategory.FraudDetected,
            ForceCancel = true
        };

        await messageBus.SendAsync(cancelCommand);
        State.Events.Add(new SagaEvent("PaymentCancelledDueToFraud", cancelCommand));

        logger.LogWarning(
            "Payment {PaymentId} cancelled due to high fraud risk [CorrelationId: {CorrelationId}]",
            paymentId, State.CorrelationId);
    }

    private async Task RequestManualReviewAsync(
        PaymentId paymentId,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        State.CurrentStep = PaymentProcessingStep.AwaitingManualReview;
        State.Status = PaymentSagaStatus.AwaitingManualReview;

        var reviewRequest = new PaymentManualReviewRequest
        {
            PaymentId = paymentId,
            PaymentProcessingSagaId = Id,
            RiskLevel = State.FraudDetectionResult?.RiskLevel ?? FraudRiskLevel.High,
            RiskScore = State.FraudDetectionResult?.RiskScore ?? 0.8m,
            Reason = "High fraud risk detected - manual review required"
        };

        await messageBus.SendAsync(reviewRequest);
        State.Events.Add(new SagaEvent("ManualReviewRequested", reviewRequest));

        logger.LogWarning(
            "Payment {PaymentId} requires manual review due to high fraud risk [CorrelationId: {CorrelationId}]",
            paymentId, State.CorrelationId);
    }

    private async Task HandleReservationFailureAsync(
        ReservePaymentResponse response,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        State.Status = PaymentSagaStatus.Failed;
        State.FailureReason = response.FailureReason ?? "Payment reservation failed";

        var failureEvent = new PaymentProcessingFailedEvent
        {
            PaymentId = response.PaymentId,
            PaymentProcessingSagaId = Id,
            FailureStep = PaymentProcessingStep.Reserving,
            Reason = State.FailureReason
        };

        await messageBus.PublishAsync(failureEvent);
        State.Events.Add(new SagaEvent("ReservationFailed", failureEvent));

        logger.LogError(
            "Payment reservation failed for {PaymentId}: {FailureReason} [CorrelationId: {CorrelationId}]",
            response.PaymentId, response.FailureReason, State.CorrelationId);
    }

    private async Task ScheduleSettlementAsync(
        PaymentId paymentId,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        var settleCommand = new SettlePaymentCommand
        {
            PaymentProcessingSagaId = Id,
            PaymentId = paymentId,
            IdempotencyKey = $"{State.IdempotencyKey}-settle",
            CorrelationId = State.CorrelationId,
            SettlementAmount = State.ReservedAmount ?? State.Amount
        };

        // Schedule settlement with a small delay to allow for any processing
        await messageBus.ScheduleAsync(settleCommand, DateTimeOffset.UtcNow.AddSeconds(5));
        State.Events.Add(new SagaEvent("SettlementScheduled", settleCommand));

        logger.LogInformation(
            "Settlement scheduled for payment {PaymentId} [CorrelationId: {CorrelationId}]",
            paymentId, State.CorrelationId);
    }

    private async Task HandleSettlementFailureAsync(
        SettlePaymentResponse response,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        State.Status = PaymentSagaStatus.Failed;
        State.FailureReason = response.FailureReason ?? "Payment settlement failed";

        var failureEvent = new PaymentProcessingFailedEvent
        {
            PaymentId = response.PaymentId,
            PaymentProcessingSagaId = Id,
            FailureStep = PaymentProcessingStep.Settling,
            Reason = State.FailureReason
        };

        await messageBus.PublishAsync(failureEvent);
        State.Events.Add(new SagaEvent("SettlementFailed", failureEvent));

        // TODO: Implement settlement failure compensation
        // - Keep reservation active for manual intervention
        // - Alert finance team
        // - Schedule retry attempts

        logger.LogError(
            "CRITICAL: Payment settlement failed for {PaymentId}: {FailureReason} [CorrelationId: {CorrelationId}]",
            response.PaymentId, response.FailureReason, State.CorrelationId);
    }

    private async Task SendCompletionNotificationsAsync(
        PaymentId paymentId,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        var completionEvent = new PaymentProcessingCompletedEvent
        {
            PaymentId = paymentId,
            CustomerId = State.CustomerId,
            MerchantId = State.MerchantId,
            Amount = State.SettledAmount ?? State.Amount,
            Currency = State.Currency,
            ProcessedAt = State.CompletedAt ?? DateTimeOffset.UtcNow,
            PaymentProcessingSagaId = Id
        };

        await messageBus.PublishAsync(completionEvent);
        State.Events.Add(new SagaEvent("CompletionNotificationSent", completionEvent));

        logger.LogInformation(
            "Payment processing completed for {PaymentId}, amount: {Amount} {Currency} [CorrelationId: {CorrelationId}]",
            paymentId, completionEvent.Amount, State.Currency, State.CorrelationId);
    }

    private async Task PerformTimeoutCompensationAsync(
        PaymentId paymentId,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        switch (State.CurrentStep)
        {
            case PaymentProcessingStep.Reserving:
            case PaymentProcessingStep.Settling:
                // Cancel payment if it's stuck in processing
                await CancelPaymentDueToTimeoutAsync(paymentId, messageBus, logger, cancellationToken);
                break;

            case PaymentProcessingStep.FraudDetection:
                // If fraud detection is taking too long, proceed with reservation (fail-safe)
                logger.LogWarning("Fraud detection timed out, proceeding without fraud check (RISK!)");
                // TODO: Implement fallback fraud detection or manual review
                break;
        }
    }

    private async Task CancelPaymentDueToTimeoutAsync(
        PaymentId paymentId,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        var cancelCommand = new CancelPaymentCommand
        {
            PaymentProcessingSagaId = Id,
            PaymentId = paymentId,
            IdempotencyKey = $"{State.IdempotencyKey}-cancel-timeout",
            CorrelationId = State.CorrelationId,
            CancellationReason = $"Payment cancelled due to timeout at step {State.CurrentStep}",
            Category = CancellationCategory.Timeout,
            ForceCancel = true
        };

        await messageBus.SendAsync(cancelCommand);
        State.Events.Add(new SagaEvent("PaymentCancelledDueToTimeout", cancelCommand));

        logger.LogWarning(
            "Payment {PaymentId} cancelled due to saga timeout [CorrelationId: {CorrelationId}]",
            paymentId, State.CorrelationId);
    }

    private async Task HandleSagaFailureAsync(
        PaymentId paymentId,
        Exception ex,
        IMessageBus messageBus,
        ILogger<PaymentProcessingSaga> logger,
        CancellationToken cancellationToken
    )
    {
        State.Status = PaymentSagaStatus.Failed;
        State.FailureReason = ex.Message;
        State.CompletedAt = DateTimeOffset.UtcNow;

        var failureEvent = new PaymentProcessingFailedEvent
        {
            PaymentId = paymentId,
            PaymentProcessingSagaId = Id,
            FailureStep = State.CurrentStep,
            Reason = ex.Message,
            Exception = ex.GetType().Name
        };

        await messageBus.PublishAsync(failureEvent);
        State.Events.Add(new SagaEvent("SagaFailed", failureEvent));

        logger.LogError(ex,
            "Payment Processing Saga failed for payment {PaymentId} [CorrelationId: {CorrelationId}]",
            paymentId, State.CorrelationId);
    }

    private Task CompleteSagaAsync(PaymentId paymentId, ILogger<PaymentProcessingSaga> logger)
    {
        MarkCompleted();

        logger.LogInformation(
            "Payment Processing Saga completed successfully for payment {PaymentId} [CorrelationId: {CorrelationId}, Duration: {Duration}]",
            paymentId, State.CorrelationId, State.CompletedAt - State.StartedAt);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps FraudDetection RiskLevel to Contracts RiskLevel
    /// </summary>
    private static Mediso.PaymentSample.Application.Modules.Payments.Contracts.RiskLevel MapFraudRiskLevelToContractsRiskLevel(
        FraudRiskLevel fraudRiskLevel
    )
    {
        return fraudRiskLevel switch
        {
            FraudRiskLevel.Low => Mediso.PaymentSample.Application.Modules.Payments.Contracts.RiskLevel.Low,
            FraudRiskLevel.Medium => Mediso.PaymentSample.Application.Modules.Payments.Contracts.RiskLevel.Medium,
            FraudRiskLevel.High => Mediso.PaymentSample.Application.Modules.Payments.Contracts.RiskLevel.High,
            FraudRiskLevel.Blocked => Mediso.PaymentSample.Application.Modules.Payments.Contracts.RiskLevel.Critical,
            _ => Mediso.PaymentSample.Application.Modules.Payments.Contracts.RiskLevel.Medium
        };
    }
}