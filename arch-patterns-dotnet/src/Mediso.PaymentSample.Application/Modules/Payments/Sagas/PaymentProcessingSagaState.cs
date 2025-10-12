using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Application.Modules.FraudDetection.Contracts;
using Wolverine.Persistence.Sagas;

namespace Mediso.PaymentSample.Application.Modules.Payments.Sagas;

/// <summary>
/// State object for Payment Processing Saga containing all necessary information
/// to manage the payment lifecycle and handle compensation scenarios.
/// </summary>
public class PaymentProcessingSagaState
{
    /// <summary>
    /// Unique identifier for the saga instance (required by Marten).
    /// </summary>
    [SagaIdentity] public Guid Id { get; set; }
    
    /// <summary>
    /// Payment identifier generated during initiation.
    /// </summary>
    public PaymentId? PaymentId { get; set; }
    
    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; }
    
    /// <summary>
    /// Idempotency key from the original request.
    /// </summary>
    public string IdempotencyKey { get; set; }
    
    /// <summary>
    /// Customer initiating the payment.
    /// </summary>
    public CustomerId CustomerId { get; set; }
    
    /// <summary>
    /// Merchant receiving the payment.
    /// </summary>
    public MerchantId MerchantId { get; set; }
    
    /// <summary>
    /// Original payment amount.
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Payment currency.
    /// </summary>
    public string Currency { get; set; }
    
    /// <summary>
    /// Payment method used.
    /// </summary>
    public string PaymentMethod { get; set; }
    
    /// <summary>
    /// Amount reserved during reservation step.
    /// </summary>
    public decimal? ReservedAmount { get; set; }
    
    /// <summary>
    /// Amount actually settled.
    /// </summary>
    public decimal? SettledAmount { get; set; }
    
    /// <summary>
    /// Current step in the payment processing saga.
    /// </summary>
    public PaymentProcessingStep CurrentStep { get; set; }
    
    /// <summary>
    /// Overall saga status.
    /// </summary>
    public PaymentSagaStatus Status { get; set; }
    
    /// <summary>
    /// Fraud detection result from the fraud detection module.
    /// </summary>
    public FraudDetectionCompletedEvent? FraudDetectionResult { get; set; }
    
    /// <summary>
    /// Saga start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
    
    /// <summary>
    /// Saga completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
    
    /// <summary>
    /// Failure reason if saga fails.
    /// </summary>
    public string? FailureReason { get; set; }
    
    /// <summary>
    /// Audit trail of events that occurred during saga execution.
    /// </summary>
    public List<SagaEvent> Events { get; set; } = new();
    
    /// <summary>
    /// Number of retry attempts for the current step.
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// Last retry timestamp.
    /// </summary>
    public DateTimeOffset? LastRetryAt { get; set; }
    
    /// <summary>
    /// Additional metadata for saga context.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Enumeration of payment processing saga steps.
/// </summary>
public enum PaymentProcessingStep
{
    /// <summary>Payment is being initiated.</summary>
    Initiating,
    
    /// <summary>Fraud detection is in progress.</summary>
    FraudDetection,
    
    /// <summary>Processing fraud detection results.</summary>
    ProcessingFraudResult,
    
    /// <summary>Payment funds are being reserved.</summary>
    Reserving,
    
    /// <summary>Payment is being settled/captured.</summary>
    Settling,
    
    /// <summary>Sending completion notifications.</summary>
    NotifyingCompletion,
    
    /// <summary>Cancelling payment due to fraud detection.</summary>
    CancellingDueToFraud,
    
    /// <summary>Awaiting manual review for high-risk payment.</summary>
    AwaitingManualReview,
    
    /// <summary>Performing compensation actions.</summary>
    Compensating,
    
    /// <summary>Saga completed successfully.</summary>
    Completed,
    
    /// <summary>Saga failed and compensation completed.</summary>
    Failed
}

/// <summary>
/// Overall status of the payment processing saga.
/// </summary>
public enum PaymentSagaStatus
{
    /// <summary>Saga has not been started.</summary>
    NotStarted,
    
    /// <summary>Saga is currently in progress.</summary>
    InProgress,
    
    /// <summary>Saga completed successfully.</summary>
    Completed,
    
    /// <summary>Saga failed with compensation completed.</summary>
    Failed,
    
    /// <summary>Saga timed out.</summary>
    TimedOut,
    
    /// <summary>Saga cancelled due to fraud detection.</summary>
    CancelledDueToFraud,
    
    /// <summary>Saga is waiting for manual review.</summary>
    AwaitingManualReview,
    
    /// <summary>Saga is performing compensation actions.</summary>
    Compensating
}

/// <summary>
/// Represents an event that occurred during saga execution for audit purposes.
/// </summary>
public record SagaEvent
{
    /// <summary>
    /// Name/type of the event.
    /// </summary>
    public string EventName { get; init; }
    
    /// <summary>
    /// Event data (serialized to JSON for storage).
    /// </summary>
    public object EventData { get; init; }
    
    /// <summary>
    /// Timestamp when event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
    
    /// <summary>
    /// Additional event metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
    
    public SagaEvent(string eventName, object eventData, Dictionary<string, string>? metadata = null)
    {
        EventName = eventName;
        EventData = eventData;
        Timestamp = DateTimeOffset.UtcNow;
        Metadata = metadata;
    }
}

// ========================================================================================
// SAGA EVENT MESSAGES
// ========================================================================================

/// <summary>
/// Timeout message for saga timeout handling.
/// </summary>
public record PaymentSagaTimeout
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    public PaymentId PaymentId { get; set; }
    
    public DateTimeOffset TimeoutAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event published when payment processing fails.
/// </summary>
public record PaymentProcessingFailedEvent
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    public PaymentId PaymentId { get; init; }
    public PaymentProcessingStep FailureStep { get; init; }
    public string Reason { get; init; }
    public string? Exception { get; init; }
    public DateTimeOffset FailedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Event published when payment processing completes successfully.
/// </summary>
public record PaymentProcessingCompletedEvent
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    public PaymentId PaymentId { get; init; }
    public CustomerId CustomerId { get; init; }
    public MerchantId MerchantId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; }
    public DateTimeOffset ProcessedAt { get; init; }
    public TimeSpan ProcessingDuration { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Request for manual review of high-risk payment.
/// </summary>
public record PaymentManualReviewRequest
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    public PaymentId PaymentId { get; init; }
    
    public RiskLevel RiskLevel { get; init; }
    public decimal RiskScore { get; init; }
    public string Reason { get; init; }
    public List<string> RiskFactors { get; init; } = new();
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Response from manual review of payment.
/// </summary>
public record PaymentManualReviewResponse
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    public PaymentId PaymentId { get; init; }
    
    public ManualReviewDecision Decision { get; init; }
    public string ReviewedBy { get; init; }
    public string? Notes { get; init; }
    public DateTimeOffset ReviewedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Manual review decision options.
/// </summary>
public enum ManualReviewDecision
{
    /// <summary>Approve payment for processing.</summary>
    Approve,
    
    /// <summary>Reject payment due to fraud concerns.</summary>
    Reject,
    
    /// <summary>Require additional verification.</summary>
    RequireAdditionalVerification,
    
    /// <summary>Escalate to senior reviewer.</summary>
    Escalate
}

