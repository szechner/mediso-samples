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
public class InitiatePaymentCommandHandler : IInitiatePaymentHandler
{
    private readonly ILogger<InitiatePaymentCommandHandler> _logger;
    private readonly IEventStore _eventStore;
    private readonly IResiliencePipelineProvider _resilienceProvider;
    
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Commands");
    
    public InitiatePaymentCommandHandler(ILogger<InitiatePaymentCommandHandler> logger, IEventStore eventStore, IResiliencePipelineProvider resilienceProvider)
    {
        _logger = logger;
        _eventStore = eventStore;
        _resilienceProvider = resilienceProvider;
    }
    
    /// <summary>
    /// Handles payment initiation with comprehensive domain validation and event sourcing.
    /// </summary>
    public async Task<InitiatePaymentResponse> HandleAsync(
        InitiatePaymentCommand command, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Command.InitiatePayment");
        activity?.SetTag(TracingConstants.CorrelationId, command.CorrelationId);
        activity?.SetTag(TracingConstants.IdempotencyKey, command.IdempotencyKey);
        activity?.SetTag("payment.amount", command.Amount);
        activity?.SetTag("payment.currency", command.Currency);
        activity?.SetTag("customer.id", command.CustomerId.Value);
        activity?.SetTag("merchant.id", command.MerchantId.Value);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing InitiatePaymentCommand for {Amount} {Currency} from customer {CustomerId} to merchant {MerchantId} [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
            command.Amount, command.Currency, command.CustomerId, command.MerchantId, 
            command.CorrelationId, command.IdempotencyKey);

        try
        {
            // Check for idempotency - has this exact request been processed before?
            var existingPayment = await CheckForExistingPaymentAsync(command, cancellationToken);
            if (existingPayment != null)
            {
                _logger.LogInformation(
                    "Idempotent request detected - returning existing payment {PaymentId} [IdempotencyKey: {IdempotencyKey}]",
                    existingPayment.Id, command.IdempotencyKey);

                activity?.SetTag("payment.is_duplicate", true);
                activity?.SetTag("payment.id", existingPayment.Id.Value);

                return new InitiatePaymentResponse
                {
                    PaymentId = existingPayment.Id,
                    Status = (PaymentStatus)existingPayment.State,
                    CorrelationId = command.CorrelationId,
                    InitiatedAt = DateTimeOffset.UtcNow, // Domain doesn't store creation time directly
                    IsDuplicateRequest = true
                };
            }

            // Create domain objects
            var paymentId = PaymentId.New();
            var money = new Money(command.Amount, new Currency(command.Currency));
            var payerAccount = new AccountId(command.CustomerId.Value.ToString());
            var payeeAccount = new AccountId(command.MerchantId.Value.ToString());

            activity?.SetTag("payment.id", paymentId.Value);

            // Create Payment aggregate using domain factory method
            var payment = Payment.Create(
                paymentId, 
                money, 
                payerAccount, 
                payeeAccount, 
                command.Description ?? string.Empty);

            // Note: Domain model doesn't have AddMetadata - metadata handled at application level

            // Persist using event sourcing with resilience
            var pipeline = _resilienceProvider.GetPipeline("event-store");
            
            await pipeline.ExecuteAsync(async ct =>
            {
                using var persistActivity = ActivitySource.StartActivity("EventStore.SaveAggregate");
                (string Key, object? Value) header = ("metadata", command.Metadata);
                await _eventStore.SaveAggregateAsync(payment, command.CorrelationId, header, ct);
                
                _logger.LogDebug(
                    "Payment {PaymentId} persisted to event store with {EventCount} domain events",
                    paymentId, payment.UncommittedEvents.Count);
                
                return Task.CompletedTask;
            }, cancellationToken);

            // Log domain events for observability
            foreach (var domainEvent in payment.UncommittedEvents)
            {
                _logger.LogInformation(
                    "Domain event raised: {EventType} for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                    domainEvent.GetType().Name, paymentId, command.CorrelationId);
                
                activity?.AddEvent(new ActivityEvent(
                    "domain_event_raised",
                    DateTimeOffset.UtcNow,
                    new ActivityTagsCollection([
                        new("event.type", domainEvent.GetType().Name),
                        new("payment.id", paymentId.Value.ToString())
                    ])));
            }

            var response = new InitiatePaymentResponse
            {
                PaymentId = paymentId,
                Status = (PaymentStatus)payment.State,
                CorrelationId = command.CorrelationId,
                InitiatedAt = DateTimeOffset.UtcNow,
                IsDuplicateRequest = false
            };

            _logger.LogInformation(
                "Successfully initiated payment {PaymentId} with status {Status} in {Duration}ms [CorrelationId: {CorrelationId}]",
                paymentId, payment.State, stopwatch.ElapsedMilliseconds, command.CorrelationId);

            activity?.SetTag("payment.status", payment.State.ToString());
            activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("payment.is_duplicate", false);

            return response;
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex,
                "Domain validation failed for InitiatePaymentCommand [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
                command.CorrelationId, command.IdempotencyKey);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "DomainValidation");
            activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);
            
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing InitiatePaymentCommand [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
                command.CorrelationId, command.IdempotencyKey);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("command.duration_ms", stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
    
    private async Task<Payment?> CheckForExistingPaymentAsync(
        InitiatePaymentCommand command, 
        CancellationToken cancellationToken)
    {
        // Simplified idempotency check - in a real system, you would:
        // 1. Use a dedicated idempotency store or database
        // 2. Hash the idempotency key for consistent lookups
        // 3. Consider command payload comparison for stronger idempotency

        try
        {
            // For this demo, skip idempotency check since repository method doesn't exist
            // In production, implement proper idempotency store
            await Task.CompletedTask;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to check for existing payment with idempotency key {IdempotencyKey} - proceeding with new payment creation",
                command.IdempotencyKey);
            
            // If idempotency check fails, proceed with creation rather than failing the request
            return null;
        }
    }
}