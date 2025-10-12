using System.Diagnostics;
using FluentValidation;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Ports.Secondary;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Application.Modules.Payments.Handlers;

/// <summary>
/// Use case for initiating payments following Clean Architecture principles.
/// Implements the primary port (IInitiatePaymentHandler) and orchestrates 
/// domain logic with infrastructure concerns through secondary ports.
/// 
/// Responsibilities:
/// - Input validation and sanitization
/// - Idempotency handling
/// - Business rule enforcement
/// - Domain model orchestration
/// - Infrastructure coordination
/// - Observability and error handling
/// </summary>
public sealed class _InitiatePaymentUseCasessss //: IInitiatePaymentHandler
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Payments");
    
    private readonly IValidator<InitiatePaymentCommand> _validator;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IPaymentNotificationService _notificationService;
    private readonly ILogger<_InitiatePaymentUseCasessss> _logger;
    
    public _InitiatePaymentUseCasessss(
        IValidator<InitiatePaymentCommand> validator,
        IPaymentRepository paymentRepository,
        IIdempotencyService idempotencyService,
        IPaymentNotificationService notificationService,
        ILogger<_InitiatePaymentUseCasessss> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles payment initiation with comprehensive validation and idempotency.
    /// Follows the use case pattern from Clean Architecture.
    /// </summary>
    public async Task<InitiatePaymentResponse> HandleAsync(
        InitiatePaymentCommand command, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("InitiatePayment");
        activity?.SetTag(TracingConstants.CorrelationId, command.CorrelationId);
        activity?.SetTag(TracingConstants.IdempotencyKey, command.IdempotencyKey);
        activity?.SetTag("payment.amount", command.Amount);
        activity?.SetTag("payment.currency", command.Currency);
        activity?.SetTag("payment.method", command.PaymentMethod);

        _logger.LogInformation(
            "Initiating payment for customer {CustomerId} to merchant {MerchantId} " +
            "with amount {Amount} {Currency} [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
            command.CustomerId, command.MerchantId, command.Amount, command.Currency,
            command.CorrelationId, command.IdempotencyKey);

        try
        {
            // Step 1: Input Validation
            await ValidateCommandAsync(command, cancellationToken);

            // Step 2: Idempotency Check
            var cachedResponse = await CheckIdempotencyAsync(command, cancellationToken);
            if (cachedResponse != null)
            {
                _logger.LogDebug(
                    "Returning cached response for idempotency key {IdempotencyKey} [CorrelationId: {CorrelationId}]",
                    command.IdempotencyKey, command.CorrelationId);
                
                activity?.SetTag("payment.is_duplicate", true);
                return cachedResponse;
            }

            // Step 3: Acquire Distributed Lock for Command Processing
            await using var lockHandle = await AcquireProcessingLockAsync(command, cancellationToken);
            
            // Step 4: Double-check idempotency after acquiring lock
            cachedResponse = await CheckIdempotencyAsync(command, cancellationToken);
            if (cachedResponse != null)
            {
                activity?.SetTag("payment.is_duplicate", true);
                return cachedResponse;
            }

            // Step 5: Create and Persist Payment Domain Aggregate
            var response = await CreatePaymentAsync(command, cancellationToken);

            // Step 6: Cache Response for Idempotency
            await CacheResponseAsync(command, response, cancellationToken);

            // Step 7: Send Notifications (Fire-and-Forget)
            _ = Task.Run(async () => await SendNotificationAsync(response, cancellationToken), 
                         CancellationToken.None);

            _logger.LogInformation(
                "Successfully initiated payment {PaymentId} with status {Status} [CorrelationId: {CorrelationId}]",
                response.PaymentId, response.Status, response.CorrelationId);

            activity?.SetTag("payment.id", response.PaymentId.Value);
            activity?.SetTag("payment.status", response.Status.ToString());
            activity?.SetTag("payment.is_duplicate", response.IsDuplicateRequest);

            return response;
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex,
                "Payment initiation failed due to validation errors [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
                command.CorrelationId, command.IdempotencyKey);
            
            activity?.SetStatus(ActivityStatusCode.Error, "Validation failed");
            activity?.SetTag("error.type", "ValidationError");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Payment initiation failed with unexpected error [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
                command.CorrelationId, command.IdempotencyKey);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            throw;
        }
    }

    private async Task ValidateCommandAsync(InitiatePaymentCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException($"Payment initiation validation failed: {errors}");
        }
    }

    private async Task<InitiatePaymentResponse?> CheckIdempotencyAsync(
        InitiatePaymentCommand command, 
        CancellationToken cancellationToken)
    {
        return await _idempotencyService.GetCachedResponseAsync<InitiatePaymentResponse>(
            command.IdempotencyKey, 
            cancellationToken);
    }

    private async Task<IAsyncDisposable?> AcquireProcessingLockAsync(
        InitiatePaymentCommand command, 
        CancellationToken cancellationToken)
    {
        var lockTimeout = command.ProcessingTimeout ?? TimeSpan.FromMinutes(2);
        return await _idempotencyService.AcquireLockAsync(
            command.IdempotencyKey, 
            lockTimeout, 
            cancellationToken);
    }

    private async Task<InitiatePaymentResponse> CreatePaymentAsync(
        InitiatePaymentCommand command, 
        CancellationToken cancellationToken)
    {
        // Create domain aggregate using domain factory methods
        var paymentId = PaymentId.New();
        var amount = new Money(command.Amount, new Currency(command.Currency));
        var payment = Payment.Create(
            paymentId,
            amount,
            new AccountId(command.CustomerId.ToString()), // Convert CustomerId to AccountId
            new AccountId(command.MerchantId.ToString()), // Convert MerchantId to AccountId
            command.Description ?? string.Empty);

        // Persist the aggregate (event sourcing)
        await _paymentRepository.SaveAsync(payment, expectedVersion: null, cancellationToken);

        // Create response
        return new InitiatePaymentResponse
        {
            PaymentId = paymentId,
            Status = (PaymentStatus)payment.State, // Direct cast since they have the same values
            CorrelationId = command.CorrelationId,
            InitiatedAt = DateTimeOffset.UtcNow,
            IsDuplicateRequest = false
        };
    }

    private async Task CacheResponseAsync(
        InitiatePaymentCommand command, 
        InitiatePaymentResponse response, 
        CancellationToken cancellationToken)
    {
        var cacheExpiration = TimeSpan.FromHours(24); // Cache for 24 hours
        await _idempotencyService.CacheResponseAsync(
            command.IdempotencyKey, 
            response, 
            cacheExpiration, 
            cancellationToken);
    }

    private async Task SendNotificationAsync(
        InitiatePaymentResponse response, 
        CancellationToken cancellationToken)
    {
        try
        {
            var notification = new PaymentStatusNotification(
                response.PaymentId,
                response.Status,
                response.CorrelationId,
                response.InitiatedAt,
                new Dictionary<string, string>
                {
                    ["EventType"] = "PaymentInitiated",
                    ["Source"] = "InitiatePaymentUseCase"
                });

            await _notificationService.SendPaymentNotificationAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send notification for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                response.PaymentId, response.CorrelationId);
            
            // Don't throw - notifications are not critical for payment processing
        }
    }
}