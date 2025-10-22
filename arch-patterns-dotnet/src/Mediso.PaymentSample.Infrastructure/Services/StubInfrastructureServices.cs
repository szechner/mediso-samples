using System.Collections.Concurrent;
using System.Diagnostics;
using Mediso.PaymentSample.Application.Modules.Payments.Ports;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.Services;

/// <summary>
/// In-memory stub implementation of IIdempotencyService for development/testing.
/// In production, this would use Redis or a database.
/// </summary>
public sealed class MemoryIdempotencyService : IIdempotencyService
{
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);
    
    private readonly ConcurrentDictionary<string, (object Response, DateTimeOffset Expiry)> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ILogger<MemoryIdempotencyService> _logger;

    public MemoryIdempotencyService(ILogger<MemoryIdempotencyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse?> GetCachedResponseAsync<TResponse>(
        string idempotencyKey, 
        CancellationToken cancellationToken = default) 
        where TResponse : class
    {
        using var activity = ActivitySource.StartActivity("IdempotencyService.GetCachedResponse");
        activity?.SetTag("idempotency.key", idempotencyKey);

        await Task.CompletedTask; // Simulate async operation

        if (_cache.TryGetValue(idempotencyKey, out var cached))
        {
            if (cached.Expiry > DateTimeOffset.UtcNow)
            {
                _logger.LogDebug("Found cached response for idempotency key {Key}", idempotencyKey);
                activity?.SetTag("idempotency.cache_hit", true);
                return cached.Response as TResponse;
            }
            else
            {
                // Remove expired entry
                _cache.TryRemove(idempotencyKey, out _);
                _logger.LogDebug("Cached response expired for idempotency key {Key}", idempotencyKey);
            }
        }

        activity?.SetTag("idempotency.cache_hit", false);
        return null;
    }

    public async Task CacheResponseAsync<TResponse>(
        string idempotencyKey, 
        TResponse response, 
        TimeSpan expiration, 
        CancellationToken cancellationToken = default) 
        where TResponse : class
    {
        using var activity = ActivitySource.StartActivity("IdempotencyService.CacheResponse");
        activity?.SetTag("idempotency.key", idempotencyKey);
        activity?.SetTag("idempotency.expiration_minutes", expiration.TotalMinutes);

        await Task.CompletedTask; // Simulate async operation

        var expiryTime = DateTimeOffset.UtcNow.Add(expiration);
        _cache.TryAdd(idempotencyKey, (response, expiryTime));
        
        _logger.LogDebug("Cached response for idempotency key {Key} until {Expiry}", idempotencyKey, expiryTime);
    }

    public async Task<IAsyncDisposable?> AcquireLockAsync(
        string idempotencyKey, 
        TimeSpan lockTimeout, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("IdempotencyService.AcquireLock");
        activity?.SetTag("idempotency.key", idempotencyKey);
        activity?.SetTag("lock.timeout_seconds", lockTimeout.TotalSeconds);

        var semaphore = _locks.GetOrAdd(idempotencyKey, _ => new SemaphoreSlim(1, 1));
        
        try
        {
            var acquired = await semaphore.WaitAsync(lockTimeout, cancellationToken);
            if (!acquired)
            {
                _logger.LogWarning("Failed to acquire lock for idempotency key {Key} within timeout", idempotencyKey);
                activity?.SetTag("lock.acquired", false);
                return null;
            }

            _logger.LogDebug("Acquired lock for idempotency key {Key}", idempotencyKey);
            activity?.SetTag("lock.acquired", true);
            
            return new LockHandle(semaphore, idempotencyKey, _logger);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Lock acquisition cancelled for idempotency key {Key}", idempotencyKey);
            activity?.SetTag("lock.cancelled", true);
            throw;
        }
    }

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly string _idempotencyKey;
        private readonly ILogger _logger;
        private bool _disposed;

        public LockHandle(SemaphoreSlim semaphore, string idempotencyKey, ILogger logger)
        {
            _semaphore = semaphore;
            _idempotencyKey = idempotencyKey;
            _logger = logger;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                _logger.LogDebug("Released lock for idempotency key {Key}", _idempotencyKey);
                _disposed = true;
            }
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Stub implementation of IPaymentProcessor for development/testing.
/// In production, this would integrate with real payment gateways.
/// </summary>
public sealed class StubPaymentProcessor : IPaymentProcessor
{
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);
    
    private readonly ILogger<StubPaymentProcessor> _logger;

    public StubPaymentProcessor(ILogger<StubPaymentProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(
        PaymentAuthorizationRequest request, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentProcessor.Authorize");
        activity?.SetTag(TracingConstants.Tags.PaymentId, request.PaymentId.ToString());
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.currency", request.Currency);

        // Simulate processing delay
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

        // Simulate successful authorization for amounts <= 10000, failure for larger amounts
        var isSuccessful = request.Amount <= 10000;
        var authCode = isSuccessful ? $"AUTH_{Guid.NewGuid():N}[..8]" : null;
        var transactionId = isSuccessful ? $"TXN_{Guid.NewGuid():N}[..12]" : null;

        _logger.LogInformation("Stub authorization for payment {PaymentId}: {Result}", 
            request.PaymentId, isSuccessful ? "Success" : "Failed");

        activity?.SetTag("authorization.successful", isSuccessful);
        
        return new PaymentAuthorizationResult(
            IsSuccessful: isSuccessful,
            AuthorizationCode: authCode,
            ProcessorTransactionId: transactionId,
            FailureReason: isSuccessful ? null : "Amount exceeds stub processor limit");
    }

    public async Task<PaymentSettlementResult> SettleAsync(
        PaymentSettlementRequest request, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentProcessor.Settle");
        activity?.SetTag(TracingConstants.Tags.PaymentId, request.PaymentId.ToString());
        activity?.SetTag("payment.settlement_amount", request.SettlementAmount);

        // Simulate processing delay
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);

        var settlementId = $"SETTLE_{Guid.NewGuid():N}[..12]";
        var fees = request.SettlementAmount * 0.029m; // 2.9% processing fee
        var netAmount = request.SettlementAmount - fees;

        _logger.LogInformation("Stub settlement for payment {PaymentId}: Success", request.PaymentId);

        activity?.SetTag("settlement.successful", true);
        
        return new PaymentSettlementResult(
            IsSuccessful: true,
            ProcessorSettlementId: settlementId,
            NetAmount: netAmount,
            ProcessingFees: fees,
            FailureReason: null);
    }

    public async Task<PaymentCancellationResult> CancelAsync(
        PaymentCancellationRequest request, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentProcessor.Cancel");
        activity?.SetTag(TracingConstants.Tags.PaymentId, request.PaymentId.ToString());

        // Simulate processing delay
        await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);

        var cancellationId = $"CANCEL_{Guid.NewGuid():N}[..12]";

        _logger.LogInformation("Stub cancellation for payment {PaymentId}: Success", request.PaymentId);

        activity?.SetTag("cancellation.successful", true);
        
        return new PaymentCancellationResult(
            IsSuccessful: true,
            ProcessorCancellationId: cancellationId,
            RequiresRefund: false,
            RefundAmount: null,
            FailureReason: null);
    }

    public async Task<PaymentProcessorStatus> GetTransactionStatusAsync(
        string processorTransactionId, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentProcessor.GetStatus");
        activity?.SetTag("processor.transaction_id", processorTransactionId);

        // Simulate processing delay
        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

        _logger.LogDebug("Stub status check for transaction {TransactionId}: Completed", processorTransactionId);

        return new PaymentProcessorStatus(
            Status: "completed",
            LastUpdated: DateTimeOffset.UtcNow);
    }
}

/// <summary>
/// Stub implementation of IPaymentNotificationService for development/testing.
/// In production, this would send real notifications via email, SMS, webhooks, etc.
/// </summary>
public sealed class StubPaymentNotificationService : IPaymentNotificationService
{
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);
    
    private readonly ILogger<StubPaymentNotificationService> _logger;

    public StubPaymentNotificationService(ILogger<StubPaymentNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendPaymentNotificationAsync(
        PaymentStatusNotification notification, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("NotificationService.SendPaymentNotification");
        activity?.SetTag(TracingConstants.Tags.PaymentId, notification.PaymentId.ToString());
        activity?.SetTag("notification.status", notification.Status.ToString());
        activity?.SetTag(TracingConstants.Tags.CorrelationId, notification.CorrelationId);

        // Simulate sending notification
        await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);

        _logger.LogInformation("Stub notification sent for payment {PaymentId} with status {Status}", 
            notification.PaymentId, notification.Status);
    }

    public async Task SendWebhookAsync(
        PaymentWebhook webhook, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("NotificationService.SendWebhook");
        activity?.SetTag(TracingConstants.Tags.PaymentId, webhook.PaymentId.ToString());
        activity?.SetTag("webhook.event_type", webhook.EventType);
        activity?.SetTag("webhook.url", webhook.WebhookUrl);

        // Simulate sending webhook
        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

        _logger.LogInformation("Stub webhook sent for payment {PaymentId} to {Url}", 
            webhook.PaymentId, webhook.WebhookUrl);
    }
}