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
public class CancelPaymentCommandHandler : ICancelPaymentHandler
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Commands");
    
    private readonly IEventStore _eventStore;
    private readonly IResiliencePipelineProvider _resilienceProvider;
    private readonly ILogger<CancelPaymentCommandHandler> _logger;

    public CancelPaymentCommandHandler(IEventStore eventStore, IResiliencePipelineProvider resilienceProvider, ILogger<CancelPaymentCommandHandler> logger)
    {
        _eventStore = eventStore;
        _resilienceProvider = resilienceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Handles payment cancellation with refund processing coordination.
    /// </summary>
    public async Task<CancelPaymentResponse> HandleAsync(
        CancelPaymentCommand command, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Command.CancelPayment");
        activity?.SetTag(TracingConstants.CorrelationId, command.CorrelationId);
        activity?.SetTag(TracingConstants.IdempotencyKey, command.IdempotencyKey);
        activity?.SetTag("payment.id", command.PaymentId.Value);
        activity?.SetTag("payment.cancellation_category", command.Category.ToString());

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing CancelPaymentCommand for payment {PaymentId} with reason: {CancellationReason} [CorrelationId: {CorrelationId}]",
            command.PaymentId, command.CancellationReason, command.CorrelationId);

        try
        {
            // Load payment aggregate
            var payment = await LoadPaymentAsync(command.PaymentId, cancellationToken);
            if (payment == null)
            {
                var errorMessage = $"Payment {command.PaymentId} not found";
                _logger.LogWarning(errorMessage);
                
                activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
                
                return new CancelPaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = PaymentStatus.Failed,
                    IsCancelled = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = errorMessage
                };
            }

            // Check if already cancelled/declined
            if (payment.State == PaymentState.Declined)
            {
                _logger.LogInformation(
                    "Payment {PaymentId} is already cancelled/declined - idempotent response [IdempotencyKey: {IdempotencyKey}]",
                    command.PaymentId, command.IdempotencyKey);

                activity?.SetTag("payment.is_duplicate", true);

                return new CancelPaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsCancelled = true,
                    CorrelationId = command.CorrelationId,
                    CancelledAt = DateTimeOffset.UtcNow,
                    IsDuplicateRequest = true
                };
            }

            // Domain business rules for cancellation
            if (payment.State == PaymentState.Settled && !command.ForceCancel)
            {
                var errorMessage = "Cannot cancel settled payment without force flag";
                _logger.LogWarning(errorMessage + " [PaymentId: {PaymentId}]", command.PaymentId);
                
                return new CancelPaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsCancelled = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = errorMessage
                };
            }

            // Perform cancellation (domain business logic)
            try
            {
                payment.Cancel(command.CancellationReason);
                
                // Persist the state change
                await SavePaymentAsync(payment, command.CorrelationId, cancellationToken);

                _logger.LogInformation(
                    "Successfully cancelled payment {PaymentId} with reason: {CancellationReason} in {Duration}ms [CorrelationId: {CorrelationId}]",
                    command.PaymentId, command.CancellationReason, stopwatch.ElapsedMilliseconds, command.CorrelationId);

                activity?.SetTag("payment.is_cancelled", true);
                activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);

                // TODO: If payment was settled, initiate refund process
                RefundInformation? refundInfo = null;
                if (payment.State == PaymentState.Declined && command.Category == CancellationCategory.CustomerRequested)
                {
                    // Placeholder for refund processing logic
                    refundInfo = new RefundInformation
                    {
                        RefundId = Guid.NewGuid().ToString(),
                        Amount = payment.Amount.Amount,
                        Status = RefundStatus.Initiated,
                        ExpectedCompletionDate = DateTimeOffset.UtcNow.AddDays(3)
                    };
                    
                    _logger.LogInformation(
                        "Refund initiated for cancelled payment {PaymentId} with amount {Amount}",
                        command.PaymentId, refundInfo.Amount);
                }

                return new CancelPaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsCancelled = true,
                    CorrelationId = command.CorrelationId,
                    CancelledAt = DateTimeOffset.UtcNow,
                    RefundInfo = refundInfo,
                    IsDuplicateRequest = false
                };
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex,
                    "Domain validation failed during cancellation for payment {PaymentId}",
                    command.PaymentId);

                activity?.SetTag("payment.is_cancelled", false);
                activity?.SetTag("error.type", "DomainValidation");

                return new CancelPaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsCancelled = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = ex.Message
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing CancelPaymentCommand for payment {PaymentId} [CorrelationId: {CorrelationId}]",
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