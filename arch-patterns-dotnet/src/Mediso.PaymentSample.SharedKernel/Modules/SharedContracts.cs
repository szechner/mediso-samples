namespace Mediso.PaymentSample.SharedKernel.Modules;

/// <summary>
/// Base contract for module communication
/// </summary>
public abstract record ModuleContract
{
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Base result contract for module responses
/// </summary>
public abstract record ModuleResult
{
    public bool IsSuccess { get; init; }
    public string[]? Errors { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    public static T Success<T>() where T : ModuleResult, new() => new() { IsSuccess = true };
    public static T Failure<T>(params string[] errors) where T : ModuleResult, new() => new() { IsSuccess = false, Errors = errors };
}

// ============ PAYMENTS MODULE CONTRACTS ============

/// <summary>
/// Shared payment status enumeration
/// </summary>
public enum SharedPaymentStatus
{
    Initiated = 1,
    ComplianceScreening = 2,
    FundsReserved = 3,
    Processing = 4,
    Settled = 5,
    Failed = 6,
    Cancelled = 7
}

/// <summary>
/// Shared payment information for inter-module communication
/// </summary>
public sealed record SharedPaymentInfo(
    Guid PaymentId,
    string PaymentReference,
    SharedPaymentStatus Status,
    decimal Amount,
    string Currency,
    Guid FromAccountId,
    Guid ToAccountId,
    string? Description = null,
    DateTimeOffset? DueDate = null,
    Dictionary<string, string>? Metadata = null
) : ModuleContract;

/// <summary>
/// Payment processing result for cross-module sharing
/// </summary>
public sealed record SharedPaymentResult : ModuleResult
{
    public Guid? PaymentId { get; init; }
    public string? PaymentReference { get; init; }
    public SharedPaymentStatus Status { get; init; }
    public string? ReasonCode { get; init; }
}

// ============ ACCOUNT MODULE CONTRACTS ============

/// <summary>
/// Account balance information for inter-module sharing
/// </summary>
public sealed record SharedAccountBalance(
    Guid AccountId,
    string AccountNumber,
    decimal AvailableBalance,
    decimal ReservedAmount,
    decimal TotalBalance,
    string Currency,
    DateTimeOffset BalanceDate
) : ModuleContract;

/// <summary>
/// Account profile for compliance and risk assessment
/// </summary>
public sealed record SharedAccountProfile(
    Guid AccountId,
    string AccountNumber,
    string AccountType,
    string CustomerType,
    string RiskLevel,
    bool IsActive,
    DateTimeOffset CreatedAt,
    Dictionary<string, string>? Attributes = null
) : ModuleContract;

/// <summary>
/// Fund reservation result
/// </summary>
public sealed record SharedReservationResult : ModuleResult
{
    public Guid? ReservationId { get; init; }
    public decimal ReservedAmount { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

// ============ COMPLIANCE MODULE CONTRACTS ============

/// <summary>
/// Compliance screening result
/// </summary>
public sealed record SharedComplianceResult : ModuleResult
{
    public string? ScreeningId { get; init; }
    public ComplianceDecision Decision { get; init; }
    public string? RiskScore { get; init; }
    public string[]? Flags { get; init; }
    public string? ReasonCode { get; init; }
    public DateTimeOffset? ReviewDate { get; init; }
}

/// <summary>
/// Compliance decision enumeration
/// </summary>
public enum ComplianceDecision
{
    Approved = 1,
    Rejected = 2,
    RequiresReview = 3,
    Pending = 4
}

/// <summary>
/// Transaction limits information
/// </summary>
public sealed record SharedTransactionLimits(
    Guid AccountId,
    decimal DailyLimit,
    decimal MonthlyLimit,
    decimal TransactionLimit,
    decimal UsedDaily,
    decimal UsedMonthly,
    DateTimeOffset LimitsDate
) : ModuleContract;

// ============ LEDGER MODULE CONTRACTS ============

/// <summary>
/// Journal entry for ledger operations
/// </summary>
public sealed record SharedJournalEntry(
    Guid EntryId,
    string Reference,
    DateTimeOffset PostedDate,
    string Description,
    decimal Amount,
    string Currency,
    Guid AccountId,
    string DebitCredit, // "DEBIT" or "CREDIT"
    Dictionary<string, string>? Metadata = null
) : ModuleContract;

/// <summary>
/// Ledger operation result
/// </summary>
public sealed record SharedLedgerResult : ModuleResult
{
    public Guid? JournalId { get; init; }
    public Guid[]? EntryIds { get; init; }
    public decimal? FinalBalance { get; init; }
}

/// <summary>
/// Account balance from ledger perspective
/// </summary>
public sealed record SharedLedgerBalance(
    Guid AccountId,
    decimal Balance,
    string Currency,
    DateTimeOffset CalculatedAt,
    int TransactionCount
) : ModuleContract;

// ============ NOTIFICATION MODULE CONTRACTS ============

/// <summary>
/// Notification severity levels
/// </summary>
public enum NotificationSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Notification for cross-module communication
/// </summary>
public sealed record SharedNotification(
    string Type,
    string Subject,
    string Content,
    NotificationSeverity Severity,
    string[]? Recipients = null,
    Dictionary<string, string>? Data = null
) : ModuleContract;

/// <summary>
/// Notification result
/// </summary>
public sealed record SharedNotificationResult : ModuleResult
{
    public Guid? NotificationId { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public string? DeliveryStatus { get; init; }
}

// ============ INTEGRATION EVENT CONTRACTS ============

/// <summary>
/// Base integration event for cross-module messaging
/// </summary>
public abstract record SharedIntegrationEvent(
    Guid EventId,
    DateTimeOffset CreatedAt,
    string EventType,
    string SourceModule
) : ModuleContract
{
    /// <summary>
    /// Version of the event schema for backward compatibility
    /// </summary>
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// Correlation identifier for tracking related events
    /// </summary>
    public new string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Additional event data as key-value pairs
    /// </summary>
    public Dictionary<string, object>? EventData { get; init; }
}

/// <summary>
/// Payment state change event
/// </summary>
public sealed record SharedPaymentStateChanged(
    Guid PaymentId,
    SharedPaymentStatus PreviousStatus,
    SharedPaymentStatus NewStatus,
    string? Reason = null
) : SharedIntegrationEvent(
    Guid.NewGuid(), 
    DateTimeOffset.UtcNow, 
    nameof(SharedPaymentStateChanged), 
    "Payments");

/// <summary>
/// Account balance change event
/// </summary>
public sealed record SharedBalanceChanged(
    Guid AccountId,
    decimal PreviousBalance,
    decimal NewBalance,
    decimal TransactionAmount,
    string TransactionType
) : SharedIntegrationEvent(
    Guid.NewGuid(), 
    DateTimeOffset.UtcNow, 
    nameof(SharedBalanceChanged), 
    "Accounts");

/// <summary>
/// High-risk activity detected event
/// </summary>
public sealed record SharedHighRiskActivityDetected(
    Guid AccountId,
    string RiskType,
    string RiskLevel,
    string Description,
    Dictionary<string, string>? RiskFactors = null
) : SharedIntegrationEvent(
    Guid.NewGuid(), 
    DateTimeOffset.UtcNow, 
    nameof(SharedHighRiskActivityDetected), 
    "Compliance");
