using System.Diagnostics;
using Mediso.PaymentSample.Application.Common.Resilience;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Ports;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace Mediso.PaymentSample.Application.Modules.Payments.Handlers;

[WolverineIgnore]
public class SettlePaymentCommandHandler : ISettlePaymentHandler
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Commands");
    
    private readonly IEventStore _eventStore;
    private readonly IResiliencePipelineProvider _resilienceProvider;
    private readonly ILogger<SettlePaymentCommandHandler> _logger;

    public SettlePaymentCommandHandler(IEventStore eventStore, IResiliencePipelineProvider resilienceProvider, ILogger<SettlePaymentCommandHandler> logger)
    {
        _eventStore = eventStore;
        _resilienceProvider = resilienceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Handles payment settlement (capture) with comprehensive validation and reconciliation.
    /// </summary>
    public async Task<SettlePaymentResponse> HandleAsync(
        SettlePaymentCommand command, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Command.SettlePayment");
        activity?.SetTag(TracingConstants.CorrelationId, command.CorrelationId);
        activity?.SetTag(TracingConstants.IdempotencyKey, command.IdempotencyKey);
        activity?.SetTag("payment.id", command.PaymentId.Value);
        activity?.SetTag("payment.settlement_amount", command.SettlementAmount);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing SettlePaymentCommand for payment {PaymentId} with amount {SettlementAmount} [CorrelationId: {CorrelationId}]",
            command.PaymentId, command.SettlementAmount, command.CorrelationId);

        try
        {
            // Load payment aggregate
            var payment = await LoadPaymentAsync(command.PaymentId, cancellationToken);
            if (payment == null)
            {
                var errorMessage = $"Payment {command.PaymentId} not found";
                _logger.LogWarning(errorMessage);
                
                activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
                
                return new SettlePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = PaymentStatus.Failed,
                    IsSettled = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = errorMessage
                };
            }

            // Check for idempotency
            if (payment.State == PaymentState.Settled)
            {
                _logger.LogInformation(
                    "Payment {PaymentId} is already settled - idempotent response [IdempotencyKey: {IdempotencyKey}]",
                    command.PaymentId, command.IdempotencyKey);

                activity?.SetTag("payment.is_duplicate", true);

                return new SettlePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsSettled = true,
                    SettledAmount = payment.Amount.Amount,
                    CorrelationId = command.CorrelationId,
                    SettledAt = DateTimeOffset.UtcNow,
                    IsDuplicateRequest = true
                };
            }

            // Validate settlement amount
            if (command.SettlementAmount > payment.Amount.Amount)
            {
                var errorMessage = $"Settlement amount {command.SettlementAmount} exceeds reserved amount {payment.Amount.Amount}";
                _logger.LogWarning(errorMessage + " [PaymentId: {PaymentId}]", command.PaymentId);
                
                activity?.SetTag("error.type", "SettlementAmountExceeded");
                
                return new SettlePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsSettled = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = errorMessage
                };
            }

            // Perform settlement (domain business logic)
            try
            {
                // Need to journal first before settling (domain business rule)
                if (payment.State != PaymentState.Journaled)
                {
                    // Create basic ledger entries for journaling
                    var settlementAmount = new Money(command.SettlementAmount, payment.Amount.Currency);
                    var ledgerEntries = new List<LedgerEntry>
                    {
                        new LedgerEntry(LedgerEntryId.New(), payment.PayerAccountId, payment.PayeeAccountId, settlementAmount),
                        new LedgerEntry(LedgerEntryId.New(), payment.PayeeAccountId, payment.PayerAccountId, settlementAmount)
                    };
                    payment.Journal(ledgerEntries);
                }
                
                // Now settle with required channel parameter
                payment.Settle("payment-processor", command.IdempotencyKey);
                
                // Handle partial settlement if amounts differ
                if (command.SettlementAmount < payment.Amount.Amount)
                {
                    _logger.LogInformation(
                        "Partial settlement detected - Settlement: {SettlementAmount}, Reserved: {ReservedAmount} [PaymentId: {PaymentId}]",
                        command.SettlementAmount, payment.Amount.Amount, command.PaymentId);
                    
                    activity?.SetTag("payment.is_partial_settlement", true);
                    activity?.SetTag("payment.partial_settlement_reason", command.PartialSettlementReason);
                }
                
                // Persist the state change
                await SavePaymentAsync(payment, command.CorrelationId, cancellationToken);

                _logger.LogInformation(
                    "Successfully settled payment {PaymentId} with amount {SettledAmount} in {Duration}ms [CorrelationId: {CorrelationId}]",
                    command.PaymentId, command.SettlementAmount, stopwatch.ElapsedMilliseconds, command.CorrelationId);

                activity?.SetTag("payment.is_settled", true);
                activity?.SetTag("payment.settled_amount", command.SettlementAmount);
                activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);

                return new SettlePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsSettled = true,
                    SettledAmount = command.SettlementAmount,
                    CorrelationId = command.CorrelationId,
                    SettledAt = DateTimeOffset.UtcNow,
                    IsDuplicateRequest = false
                };
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex,
                    "Domain validation failed during settlement for payment {PaymentId}",
                    command.PaymentId);

                activity?.SetTag("payment.is_settled", false);
                activity?.SetTag("error.type", "DomainValidation");

                return new SettlePaymentResponse
                {
                    PaymentId = command.PaymentId,
                    Status = (PaymentStatus)payment.State,
                    IsSettled = false,
                    CorrelationId = command.CorrelationId,
                    FailureReason = ex.Message
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing SettlePaymentCommand for payment {PaymentId} [CorrelationId: {CorrelationId}]",
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