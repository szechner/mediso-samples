using System.Diagnostics;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Wolverine.Persistence.Sagas;

namespace Mediso.PaymentSample.Application.Modules.Payments.Contracts;

// ========================================================================================
// INITIATE PAYMENT COMMAND
// ========================================================================================

/// <summary>
/// Command to initiate a new payment with comprehensive validation and observability.
/// Supports idempotency through IdempotencyKey and includes correlation tracking.
/// Part of the Payments module in the modular monolith architecture.
/// </summary>
public record InitiatePaymentCommand
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }

    /// <summary>
    /// Unique identifier for idempotency control. 
    /// Multiple requests with the same key will result in the same outcome.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing and request tracking.
    /// </summary>
    public string CorrelationId { get; init; } = Activity.Current?.Id ?? Guid.NewGuid().ToString();

    /// <summary>
    /// Customer identifier initiating the payment.
    /// </summary>
    public required CustomerId CustomerId { get; init; }

    /// <summary>
    /// Merchant receiving the payment.
    /// </summary>
    public required MerchantId MerchantId { get; init; }

    /// <summary>
    /// Payment amount in the smallest currency unit (e.g., cents for USD).
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// ISO 4217 currency code (e.g., USD, EUR, CZK).
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Optional payment description for audit and display purposes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Payment method information (e.g., card, bank transfer, wallet).
    /// </summary>
    public required string PaymentMethod { get; init; }

    /// <summary>
    /// Optional metadata for additional payment context.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Request timestamp for ordering and timeout calculations.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional timeout for payment processing (defaults to system configuration).
    /// </summary>
    public TimeSpan? ProcessingTimeout { get; init; }
}

/// <summary>
/// Response for InitiatePaymentCommand with created payment information.
/// </summary>
public record InitiatePaymentResponse
{
    /// <summary>
    /// Generated payment identifier.
    /// </summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>
    /// Current payment status.
    /// </summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when payment was initiated.
    /// </summary>
    public required DateTimeOffset InitiatedAt { get; init; }

    /// <summary>
    /// Whether this was a duplicate request handled by idempotency.
    /// </summary>
    public bool IsDuplicateRequest { get; init; }
}

// ========================================================================================
// RESERVE PAYMENT COMMAND
// ========================================================================================

/// <summary>
/// Command to reserve funds for a payment with fraud detection and risk assessment.
/// Includes comprehensive validation and timeout handling.
/// </summary>
public record ReservePaymentCommand
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    /// <summary>
    /// Payment identifier to reserve.
    /// </summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>
    /// Idempotency key for duplicate request handling.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; init; } = Activity.Current?.Id ?? Guid.NewGuid().ToString();

    /// <summary>
    /// Risk assessment score from external fraud detection system.
    /// </summary>
    public decimal? RiskScore { get; init; }

    /// <summary>
    /// Fraud detection results and recommendations.
    /// </summary>
    public FraudDetectionResult? FraudDetection { get; init; }

    /// <summary>
    /// Payment authorization details from payment processor.
    /// </summary>
    public PaymentAuthorizationDetails? AuthorizationDetails { get; init; }

    /// <summary>
    /// Request timestamp for timeout calculations.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Maximum time to wait for reservation completion.
    /// </summary>
    public TimeSpan? ReservationTimeout { get; init; }
}

/// <summary>
/// Response for ReservePaymentCommand with reservation details.
/// </summary>
public record ReservePaymentResponse
{
    /// <summary>
    /// Payment identifier.
    /// </summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>
    /// Current payment status after reservation attempt.
    /// </summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>
    /// Whether reservation was successful.
    /// </summary>
    public required bool IsReserved { get; init; }

    /// <summary>
    /// Reserved amount (may differ from requested amount).
    /// </summary>
    public decimal? ReservedAmount { get; init; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Reservation timestamp.
    /// </summary>
    public DateTimeOffset? ReservedAt { get; init; }

    /// <summary>
    /// Reason for reservation failure (if applicable).
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Whether this was handled as a duplicate request.
    /// </summary>
    public bool IsDuplicateRequest { get; init; }
}

// ========================================================================================
// SETTLE PAYMENT COMMAND
// ========================================================================================

/// <summary>
/// Command to settle (capture) a reserved payment.
/// </summary>
public record SettlePaymentCommand
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    /// <summary>
    /// Payment identifier to settle.
    /// </summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>
    /// Idempotency key for duplicate request handling.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; init; } = Activity.Current?.Id ?? Guid.NewGuid().ToString();

    /// <summary>
    /// Settlement amount (must not exceed reserved amount).
    /// </summary>
    public required decimal SettlementAmount { get; init; }

    /// <summary>
    /// Reason for partial settlement (if amount differs from reservation).
    /// </summary>
    public string? PartialSettlementReason { get; init; }

    /// <summary>
    /// Settlement details from payment processor.
    /// </summary>
    public SettlementDetails? SettlementDetails { get; init; }

    /// <summary>
    /// Request timestamp.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Maximum time to wait for settlement completion.
    /// </summary>
    public TimeSpan? SettlementTimeout { get; init; }
}

/// <summary>
/// Response for SettlePaymentCommand with settlement information.
/// </summary>
public record SettlePaymentResponse
{
    /// <summary>
    /// Payment identifier.
    /// </summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>
    /// Current payment status after settlement attempt.
    /// </summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>
    /// Whether settlement was successful.
    /// </summary>
    public required bool IsSettled { get; init; }

    /// <summary>
    /// Actual settled amount.
    /// </summary>
    public decimal? SettledAmount { get; init; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Settlement timestamp.
    /// </summary>
    public DateTimeOffset? SettledAt { get; init; }

    /// <summary>
    /// Reason for settlement failure (if applicable).
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Whether this was handled as a duplicate request.
    /// </summary>
    public bool IsDuplicateRequest { get; init; }
}

// ========================================================================================
// CANCEL PAYMENT COMMAND
// ========================================================================================

/// <summary>
/// Command to cancel a payment (works for both initiated and reserved payments).
/// </summary>
public record CancelPaymentCommand
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    /// <summary>
    /// Payment identifier to cancel.
    /// </summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>
    /// Idempotency key for duplicate request handling.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; init; } = Activity.Current?.Id ?? Guid.NewGuid().ToString();

    /// <summary>
    /// Reason for cancellation (required for audit purposes).
    /// </summary>
    public required string CancellationReason { get; init; }

    /// <summary>
    /// Category of cancellation for analytics.
    /// </summary>
    public CancellationCategory Category { get; init; } = CancellationCategory.CustomerRequested;

    /// <summary>
    /// Whether to force cancellation even if payment is in processing state.
    /// </summary>
    public bool ForceCancel { get; init; }

    /// <summary>
    /// Cancellation details from external systems.
    /// </summary>
    public CancellationDetails? CancellationDetails { get; init; }

    /// <summary>
    /// Request timestamp.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Response for CancelPaymentCommand with cancellation information.
/// </summary>
public record CancelPaymentResponse
{
    /// <summary>
    /// Payment identifier.
    /// </summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>
    /// Current payment status after cancellation attempt.
    /// </summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>
    /// Whether cancellation was successful.
    /// </summary>
    public required bool IsCancelled { get; init; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Cancellation timestamp.
    /// </summary>
    public DateTimeOffset? CancelledAt { get; init; }

    /// <summary>
    /// Reason for cancellation failure (if applicable).
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Whether this was handled as a duplicate request.
    /// </summary>
    public bool IsDuplicateRequest { get; init; }

    /// <summary>
    /// Information about refund processing (if applicable).
    /// </summary>
    public RefundInformation? RefundInfo { get; init; }
}

// ========================================================================================
// SUPPORTING DATA STRUCTURES
// ========================================================================================

/// <summary>
/// Fraud detection result from external systems.
/// </summary>
public record FraudDetectionResult
{
    /// <summary>
    /// Overall risk level assessment.
    /// </summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Detailed risk score (0.0 to 1.0, where 1.0 is highest risk).
    /// </summary>
    public required decimal Score { get; init; }

    /// <summary>
    /// Specific risk factors identified.
    /// </summary>
    public List<string> RiskFactors { get; init; } = new();

    /// <summary>
    /// Recommended actions based on risk assessment.
    /// </summary>
    public List<string> Recommendations { get; init; } = new();

    /// <summary>
    /// Provider that performed the fraud detection.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Timestamp of fraud detection analysis.
    /// </summary>
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Risk levels for fraud detection.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Payment authorization details from payment processor.
/// </summary>
public record PaymentAuthorizationDetails
{
    /// <summary>
    /// Authorization code from payment processor.
    /// </summary>
    public required string AuthorizationCode { get; init; }

    /// <summary>
    /// Payment processor transaction ID.
    /// </summary>
    public required string ProcessorTransactionId { get; init; }

    /// <summary>
    /// AVS (Address Verification System) result.
    /// </summary>
    public string? AvsResult { get; init; }

    /// <summary>
    /// CVV verification result.
    /// </summary>
    public string? CvvResult { get; init; }

    /// <summary>
    /// Authorized amount (may differ from requested amount).
    /// </summary>
    public required decimal AuthorizedAmount { get; init; }

    /// <summary>
    /// Authorization timestamp.
    /// </summary>
    public DateTimeOffset AuthorizedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Authorization expiry time.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Settlement details from payment processor.
/// </summary>
public record SettlementDetails
{
    /// <summary>
    /// Settlement transaction ID from payment processor.
    /// </summary>
    public required string ProcessorSettlementId { get; init; }

    /// <summary>
    /// Settlement batch ID for reconciliation.
    /// </summary>
    public string? BatchId { get; init; }

    /// <summary>
    /// Net amount after fees and adjustments.
    /// </summary>
    public required decimal NetAmount { get; init; }

    /// <summary>
    /// Processing fees deducted.
    /// </summary>
    public decimal ProcessingFees { get; init; }

    /// <summary>
    /// Expected settlement date for funds transfer.
    /// </summary>
    public DateTimeOffset? ExpectedSettlementDate { get; init; }

    /// <summary>
    /// Settlement currency (may differ from original payment currency).
    /// </summary>
    public string? SettlementCurrency { get; init; }

    /// <summary>
    /// Exchange rate applied (if currency conversion occurred).
    /// </summary>
    public decimal? ExchangeRate { get; init; }
}

/// <summary>
/// Categories for payment cancellation.
/// </summary>
public enum CancellationCategory
{
    CustomerRequested,
    MerchantRequested,
    FraudDetected,
    InsufficientFunds,
    TechnicalError,
    Timeout,
    ComplianceViolation,
    SystemMaintenance
}

/// <summary>
/// Cancellation details from external systems.
/// </summary>
public record CancellationDetails
{
    /// <summary>
    /// Cancellation transaction ID from payment processor.
    /// </summary>
    public string? ProcessorCancellationId { get; init; }

    /// <summary>
    /// Whether funds were actually charged and need to be refunded.
    /// </summary>
    public bool RequiresRefund { get; init; }

    /// <summary>
    /// Refund amount if different from original payment amount.
    /// </summary>
    public decimal? RefundAmount { get; init; }

    /// <summary>
    /// Expected refund processing time.
    /// </summary>
    public TimeSpan? ExpectedRefundDuration { get; init; }

    /// <summary>
    /// Additional context for the cancellation.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Information about refund processing for cancelled payments.
/// </summary>
public record RefundInformation
{
    /// <summary>
    /// Refund transaction ID.
    /// </summary>
    public required string RefundId { get; init; }

    /// <summary>
    /// Refund amount.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Expected refund completion date.
    /// </summary>
    public DateTimeOffset? ExpectedCompletionDate { get; init; }

    /// <summary>
    /// Refund status.
    /// </summary>
    public RefundStatus Status { get; init; } = RefundStatus.Initiated;
}

/// <summary>
/// Refund processing status.
/// </summary>
public enum RefundStatus
{
    Initiated,
    Processing,
    Completed,
    Failed,
    Cancelled
}