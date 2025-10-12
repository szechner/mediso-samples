using System.Diagnostics;
using Mediso.PaymentSample.Application.Common.Resilience;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Ports.Primary;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace Mediso.PaymentSample.Application.Modules.Payments.Handlers;

[WolverineIgnore]
public class ReservePaymentCommandHandler : IReservePaymentHandler
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Commands");
    
    private readonly IEventStore _eventStore;
    private readonly IResiliencePipelineProvider _resilienceProvider;
    private readonly ILogger<ReservePaymentCommandHandler> _logger;

    public ReservePaymentCommandHandler(IEventStore eventStore, IResiliencePipelineProvider resilienceProvider, ILogger<ReservePaymentCommandHandler> logger)
    {
        _eventStore = eventStore;
        _resilienceProvider = resilienceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Handles payment reservation with fraud detection integration and authorization.
    /// </summary>
    public async Task<ReservePaymentResponse> HandleAsync(
        ReservePaymentCommand command, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"Command.ReservePayment");
        activity?.SetTag(TracingConstants.CorrelationId, command.CorrelationId);
        activity?.SetTag(TracingConstants.IdempotencyKey, command.IdempotencyKey);
        activity?.SetTag("payment.id", command.PaymentId.Value);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing ReservePaymentCommand for payment {PaymentId} [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
            command.PaymentId, command.CorrelationId, command.IdempotencyKey);

        try
        {
            // Load payment aggregate from event store
            var payment = await LoadPaymentAsync(command.PaymentId, cancellationToken);
            if (payment == null)
            {
                var errorMessage = $"Payment {command.PaymentId} not found";
                _logger.LogWarning(errorMessage);
                
                activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
                
                return new ReservePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = PaymentStatus.Failed,
                    IsReserved = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = errorMessage
                };
            }

            // Check for idempotency
            if (payment.State == PaymentState.Reserved)
            {
                _logger.LogInformation(
                    "Payment {PaymentId} is already reserved - idempotent response [IdempotencyKey: {IdempotencyKey}]",
                    command.PaymentId, command.IdempotencyKey);

                activity?.SetTag("payment.is_duplicate", true);

                return new ReservePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsReserved = true,
                    ReservedAmount = payment.Amount.Amount,
                    CorrelationId = command.CorrelationId,
                    ReservedAt = DateTimeOffset.UtcNow,
                    IsDuplicateRequest = true
                };
            }

            // Apply fraud detection results if provided
            if (command.FraudDetection != null)
            {
                _logger.LogInformation(
                    "Applying fraud detection results - Risk Level: {RiskLevel}, Score: {RiskScore} [PaymentId: {PaymentId}]",
                    command.FraudDetection.RiskLevel, command.FraudDetection.Score, command.PaymentId);

                activity?.SetTag("fraud.risk_level", command.FraudDetection.RiskLevel.ToString());
                activity?.SetTag("fraud.risk_score", command.FraudDetection.Score);

                // Domain business rules: reject high-risk payments
                if (command.FraudDetection.RiskLevel == Contracts.RiskLevel.Critical)
                {
                    var rejectionReason = "Payment rejected due to high fraud risk";
                    payment.Decline(rejectionReason);
                    
                    await SavePaymentAsync(payment, command.CorrelationId, cancellationToken);
                    
                    _logger.LogWarning(
                        "Payment {PaymentId} rejected due to high fraud risk [RiskScore: {RiskScore}]",
                        command.PaymentId, command.FraudDetection.Score);

                    return new ReservePaymentResponse
                    {
                        PaymentId = command.PaymentId,
                        Status = (PaymentStatus)payment.State,
                        IsReserved = false,
                        CorrelationId = command.CorrelationId,
                        FailureReason = rejectionReason
                    };
                }
            }

            // Perform reservation (domain business logic)
            try
            {
                // Use domain model's ReserveFunds method with a reservation ID
                var reservationId = new ReservationId(Guid.NewGuid());
                payment.ReserveFunds(reservationId);
                
                // Persist the state change
                await SavePaymentAsync(payment, command.CorrelationId, cancellationToken);

                _logger.LogInformation(
                    "Successfully reserved payment {PaymentId} with amount {ReservedAmount} in {Duration}ms [CorrelationId: {CorrelationId}]",
                    command.PaymentId, payment.Amount.Amount, stopwatch.ElapsedMilliseconds, command.CorrelationId);

                activity?.SetTag("payment.is_reserved", true);
                activity?.SetTag("payment.reserved_amount", payment.Amount.Amount);
                activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);

                return new ReservePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsReserved = true,
                    ReservedAmount = payment.Amount.Amount,
                    CorrelationId = command.CorrelationId,
                    ReservedAt = DateTimeOffset.UtcNow,
                    IsDuplicateRequest = false
                };
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex,
                    "Domain validation failed during reservation for payment {PaymentId}",
                    command.PaymentId);

                activity?.SetTag("payment.is_reserved", false);
                activity?.SetTag("error.type", "DomainValidation");

                return new ReservePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsReserved = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = ex.Message
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing ReservePaymentCommand for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                command.PaymentId, command.CorrelationId);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
    
    private async Task<Payment?> LoadPaymentAsync(PaymentId paymentId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PaymentCommandHandlers.LoadPayment");
        activity?.SetTag("payment.id", paymentId.Value);

        var pipeline = _resilienceProvider.GetPipeline("event-store");
        
        return await pipeline.ExecuteAsync(async ct =>
        {
            using var loadActivity = ActivitySource.StartActivity("EventStore.LoadAggregate");
            return await _eventStore.LoadAggregateAsync<Payment>(paymentId, ct);
        }, cancellationToken);
    }
    
    private async Task SavePaymentAsync(Payment payment, string correlationId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PaymentCommandHandlers.SavePayment");
        activity?.SetTag("payment.id", payment.Id.Value);
        activity?.SetTag("payment.event_count", payment.UncommittedEvents.Count);

        var pipeline = _resilienceProvider.GetPipeline("event-store");
        
        await pipeline.ExecuteAsync(async ct =>
        {
            using var saveActivity = ActivitySource.StartActivity("EventStore.SaveAggregate");
            await _eventStore.SaveAggregateAsync(payment, correlationId, cancellationToken: ct);
            
            _logger.LogDebug(
                "Payment {PaymentId} saved to event store with {EventCount} new events",
                payment.Id, payment.UncommittedEvents.Count);
            
            return Task.CompletedTask;
        }, cancellationToken);
    }
}