using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;

namespace Mediso.PaymentSample.Application.Modules.Payments.Ports;

/// <summary>
/// Secondary port for payment repository operations (driven adapter interface).
/// Abstracts the persistence layer following hexagonal architecture principles.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Retrieves a payment by its identifier with optional projection.
    /// </summary>
    /// <param name="paymentId">Payment identifier</param>
    /// <param name="asOfDate">Optional point-in-time for event sourcing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment aggregate or null if not found</returns>
    Task<Payment?> GetByIdAsync(
        PaymentId paymentId, 
        DateTimeOffset? asOfDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves payment aggregate with optimistic concurrency control.
    /// </summary>
    /// <param name="payment">Payment aggregate to save</param>
    /// <param name="expectedVersion">Expected version for concurrency control</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(
        Payment payment, 
        int? expectedVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if payment exists without loading the full aggregate.
    /// </summary>
    /// <param name="paymentId">Payment identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if payment exists</returns>
    Task<bool> ExistsAsync(
        PaymentId paymentId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current payment status without loading full aggregate.
    /// Optimized for status checks and business rule validation.
    /// </summary>
    /// <param name="paymentId">Payment identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current payment status or null if not found</returns>
    Task<PaymentStatus?> GetStatusAsync(
        PaymentId paymentId, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Secondary port for idempotency management.
/// Ensures commands are processed exactly once.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if a command has already been processed.
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Previous response if already processed, null otherwise</returns>
    Task<TResponse?> GetCachedResponseAsync<TResponse>(
        string idempotencyKey, 
        CancellationToken cancellationToken = default) 
        where TResponse : class;

    /// <summary>
    /// Caches the response for future idempotency checks.
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="response">Response to cache</param>
    /// <param name="expiration">Cache expiration time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CacheResponseAsync<TResponse>(
        string idempotencyKey, 
        TResponse response, 
        TimeSpan expiration,
        CancellationToken cancellationToken = default) 
        where TResponse : class;

    /// <summary>
    /// Acquires a distributed lock for command processing.
    /// Prevents concurrent processing of the same idempotency key.
    /// </summary>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="lockTimeout">Maximum lock duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Lock handle for resource cleanup</returns>
    Task<IAsyncDisposable?> AcquireLockAsync(
        string idempotencyKey, 
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Secondary port for payment processor integration.
/// Abstracts external payment gateway operations.
/// </summary>
public interface IPaymentProcessor
{
    /// <summary>
    /// Authorizes payment with the external processor.
    /// </summary>
    /// <param name="request">Authorization request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authorization result with processor details</returns>
    Task<PaymentAuthorizationResult> AuthorizeAsync(
        PaymentAuthorizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures (settles) a previously authorized payment.
    /// </summary>
    /// <param name="request">Settlement request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Settlement result with reconciliation data</returns>
    Task<PaymentSettlementResult> SettleAsync(
        PaymentSettlementRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels or refunds a payment transaction.
    /// </summary>
    /// <param name="request">Cancellation request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancellation result with refund information</returns>
    Task<PaymentCancellationResult> CancelAsync(
        PaymentCancellationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets transaction status from the payment processor.
    /// Used for reconciliation and status verification.
    /// </summary>
    /// <param name="processorTransactionId">Processor transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current transaction status</returns>
    Task<PaymentProcessorStatus> GetTransactionStatusAsync(
        string processorTransactionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Secondary port for notification services.
/// Handles communication with customers and merchants.
/// </summary>
public interface IPaymentNotificationService
{
    /// <summary>
    /// Sends payment status notification to relevant parties.
    /// </summary>
    /// <param name="notification">Notification details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendPaymentNotificationAsync(
        PaymentStatusNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends webhook notification to merchant systems.
    /// </summary>
    /// <param name="webhook">Webhook payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendWebhookAsync(
        PaymentWebhook webhook,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Supporting data structures for payment processor integration.
/// </summary>
public record PaymentAuthorizationRequest(
    PaymentId PaymentId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    Dictionary<string, string>? Metadata = null);

public record PaymentAuthorizationResult(
    bool IsSuccessful,
    string? AuthorizationCode,
    string? ProcessorTransactionId,
    string? FailureReason,
    PaymentAuthorizationDetails? Details = null);

public record PaymentSettlementRequest(
    PaymentId PaymentId,
    string ProcessorTransactionId,
    decimal SettlementAmount,
    Dictionary<string, string>? Metadata = null);

public record PaymentSettlementResult(
    bool IsSuccessful,
    string? ProcessorSettlementId,
    decimal? NetAmount,
    decimal? ProcessingFees,
    string? FailureReason,
    SettlementDetails? Details = null);

public record PaymentCancellationRequest(
    PaymentId PaymentId,
    string? ProcessorTransactionId,
    string Reason,
    Dictionary<string, string>? Metadata = null);

public record PaymentCancellationResult(
    bool IsSuccessful,
    string? ProcessorCancellationId,
    bool RequiresRefund,
    decimal? RefundAmount,
    string? FailureReason,
    CancellationDetails? Details = null);

public record PaymentProcessorStatus(
    string Status,
    DateTimeOffset LastUpdated,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// Notification data structures.
/// </summary>
public record PaymentStatusNotification(
    PaymentId PaymentId,
    PaymentStatus Status,
    string CorrelationId,
    DateTimeOffset Timestamp,
    Dictionary<string, string>? Metadata = null);

public record PaymentWebhook(
    PaymentId PaymentId,
    string EventType,
    object Payload,
    string WebhookUrl,
    Dictionary<string, string>? Headers = null);