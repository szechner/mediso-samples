using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;

namespace Mediso.PaymentSample.Application.Modules.Payments.Contracts;

// ========================================================================================
// PAYMENT QUERY CONTRACTS (READ SIDE)
// ========================================================================================

/// <summary>
/// Query to get payment status by correlation ID.
/// </summary>
public record GetPaymentStatusQuery
{
    /// <summary>
    /// CorrelationId identifier to retrieve.
    /// </summary>
    public required string? CorrelationId { get; init; }
}

/// <summary>
/// Response for payment status queries using correlation ID.
/// Provides information about the current state of asynchronous payment processing.
/// </summary>
public record PaymentSagaStatusResponse
{
    /// <summary>Correlation ID used for tracking.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Current processing status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Payment ID if available (after processing starts).</summary>
    public string? PaymentId { get; init; }

    /// <summary>Any error message if processing failed.</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>Current step in the processing workflow.</summary>
    public string CurrentStep { get; init; } = string.Empty;
    
    /// <summary>Timestamp when the request was received.</summary>
    public DateTimeOffset StartedAt { get; init; }
    
    /// <summary>Timestamp of the last status update.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Query to get final payment status by correlation ID after completion.
/// </summary>
public record PaymentCompletedStatusResponse
{
    /// <summary>Correlation ID used for tracking.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Current processing status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Payment ID if available (after processing starts).</summary>
    public Guid[] PaymentIds { get; init; } = [];
}

/// <summary>
/// Query to get payment details by payment ID.
/// Supports projection options for optimized data retrieval.
/// </summary>
public record GetPaymentQuery
{
    /// <summary>
    /// Payment identifier to retrieve.
    /// </summary>
    public required PaymentId PaymentId { get; init; }
    
    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Optional point-in-time for historical queries.
    /// </summary>
    public DateTimeOffset? AsOfDate { get; init; }
    
    /// <summary>
    /// Projection options for optimizing data retrieval.
    /// </summary>
    public PaymentProjection Projection { get; init; } = PaymentProjection.Summary;
    
    /// <summary>
    /// Whether to include sensitive information (requires elevated permissions).
    /// </summary>
    public bool IncludeSensitiveData { get; init; }
}

/// <summary>
/// Response containing payment details with requested projection.
/// </summary>
public record GetPaymentResponse
{
    /// <summary>
    /// Payment identifier.
    /// </summary>
    public required PaymentId PaymentId { get; init; }
    
    /// <summary>
    /// Current payment status.
    /// </summary>
    public required PaymentStatus Status { get; init; }
    
    /// <summary>
    /// Customer information (projection-dependent).
    /// </summary>
    public CustomerInfo? Customer { get; init; }
    
    /// <summary>
    /// Merchant information (projection-dependent).
    /// </summary>
    public MerchantInfo? Merchant { get; init; }
    
    /// <summary>
    /// Payment amount details.
    /// </summary>
    public required PaymentAmountInfo Amount { get; init; }
    
    /// <summary>
    /// Payment timestamps.
    /// </summary>
    public required PaymentTimestamps Timestamps { get; init; }
    
    /// <summary>
    /// Payment method information.
    /// </summary>
    public string? PaymentMethod { get; init; }
    
    /// <summary>
    /// Payment description.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Payment metadata (filtered based on projection).
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
    
    /// <summary>
    /// Processing details (included in detailed projections).
    /// </summary>
    public PaymentProcessingInfo? ProcessingInfo { get; init; }
    
    /// <summary>
    /// Audit trail (included in audit projections).
    /// </summary>
    public List<PaymentAuditEntry>? AuditTrail { get; init; }
    
    /// <summary>
    /// Correlation ID from the original query.
    /// </summary>
    public required string CorrelationId { get; init; }
    
    /// <summary>
    /// Timestamp when data was retrieved.
    /// </summary>
    public DateTimeOffset RetrievedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Query to search payments with filtering and pagination.
/// </summary>
public record SearchPaymentsQuery
{
    /// <summary>
    /// Search criteria for filtering payments.
    /// </summary>
    public required PaymentSearchCriteria Criteria { get; init; }
    
    /// <summary>
    /// Pagination parameters.
    /// </summary>
    public PaginationParams Pagination { get; init; } = new();
    
    /// <summary>
    /// Sorting parameters.
    /// </summary>
    public PaymentSortOptions Sort { get; init; } = new();
    
    /// <summary>
    /// Projection options for result optimization.
    /// </summary>
    public PaymentProjection Projection { get; init; } = PaymentProjection.Summary;
    
    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Response containing paginated search results.
/// </summary>
public record SearchPaymentsResponse
{
    /// <summary>
    /// Matching payments with requested projection.
    /// </summary>
    public required List<PaymentSummary> Payments { get; init; }
    
    /// <summary>
    /// Pagination metadata.
    /// </summary>
    public required PaginationMetadata Pagination { get; init; }
    
    /// <summary>
    /// Search execution metadata.
    /// </summary>
    public required SearchMetadata SearchMetadata { get; init; }
    
    /// <summary>
    /// Correlation ID from the original query.
    /// </summary>
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Query to get payment statistics and analytics.
/// </summary>
public record GetPaymentStatsQuery
{
    /// <summary>
    /// Time range for statistics calculation.
    /// </summary>
    public required DateTimeOffset FromDate { get; init; }
    
    /// <summary>
    /// End date for statistics calculation.
    /// </summary>
    public required DateTimeOffset ToDate { get; init; }
    
    /// <summary>
    /// Optional customer filter.
    /// </summary>
    public CustomerId? CustomerId { get; init; }
    
    /// <summary>
    /// Optional merchant filter.
    /// </summary>
    public MerchantId? MerchantId { get; init; }
    
    /// <summary>
    /// Grouping options for statistics.
    /// </summary>
    public PaymentStatsGrouping Grouping { get; init; } = PaymentStatsGrouping.Daily;
    
    /// <summary>
    /// Metrics to include in the response.
    /// </summary>
    public PaymentMetrics Metrics { get; init; } = PaymentMetrics.All;
    
    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Response containing payment statistics and analytics.
/// </summary>
public record GetPaymentStatsResponse
{
    /// <summary>
    /// Time period covered by statistics.
    /// </summary>
    public required DateTimeOffset FromDate { get; init; }
    
    /// <summary>
    /// End date of statistics period.
    /// </summary>
    public required DateTimeOffset ToDate { get; init; }
    
    /// <summary>
    /// Overall payment statistics.
    /// </summary>
    public required PaymentStatsSummary Summary { get; init; }
    
    /// <summary>
    /// Time-series data grouped by specified interval.
    /// </summary>
    public List<PaymentStatsGroup>? GroupedStats { get; init; }
    
    /// <summary>
    /// Status distribution statistics.
    /// </summary>
    public Dictionary<PaymentStatus, int>? StatusDistribution { get; init; }
    
    /// <summary>
    /// Payment method distribution.
    /// </summary>
    public Dictionary<string, PaymentMethodStats>? PaymentMethodStats { get; init; }
    
    /// <summary>
    /// Currency distribution.
    /// </summary>
    public Dictionary<string, CurrencyStats>? CurrencyStats { get; init; }
    
    /// <summary>
    /// Correlation ID from the original query.
    /// </summary>
    public required string CorrelationId { get; init; }
}

// ========================================================================================
// SUPPORTING DATA STRUCTURES
// ========================================================================================

/// <summary>
/// Projection options for payment queries.
/// </summary>
public enum PaymentProjection
{
    /// <summary>Basic payment information only.</summary>
    Summary,
    
    /// <summary>Summary plus detailed processing information.</summary>
    Detailed,
    
    /// <summary>All information including sensitive data.</summary>
    Full,
    
    /// <summary>Audit trail and compliance information.</summary>
    Audit
}

/// <summary>
/// Customer information projection.
/// </summary>
public record CustomerInfo
{
    public required CustomerId CustomerId { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Merchant information projection.
/// </summary>
public record MerchantInfo
{
    public required MerchantId MerchantId { get; init; }
    public string? Name { get; init; }
    public string? Category { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Payment amount information.
/// </summary>
public record PaymentAmountInfo
{
    public required decimal OriginalAmount { get; init; }
    public required string Currency { get; init; }
    public decimal? ReservedAmount { get; init; }
    public decimal? SettledAmount { get; init; }
    public decimal? RefundedAmount { get; init; }
    public decimal? FeesAmount { get; init; }
}

/// <summary>
/// Payment timestamp information.
/// </summary>
public record PaymentTimestamps
{
    public required DateTimeOffset InitiatedAt { get; init; }
    public DateTimeOffset? ReservedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; init; }
}

/// <summary>
/// Payment processing information.
/// </summary>
public record PaymentProcessingInfo
{
    public string? ProcessorTransactionId { get; init; }
    public string? AuthorizationCode { get; init; }
    public string? SettlementId { get; init; }
    public decimal? ProcessingFees { get; init; }
    public RiskLevel? RiskLevel { get; init; }
    public List<string>? ProcessingNotes { get; init; }
}

/// <summary>
/// Payment audit trail entry.
/// </summary>
public record PaymentAuditEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string EventType { get; init; }
    public required string Actor { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Payment search criteria.
/// </summary>
public record PaymentSearchCriteria
{
    public CustomerId? CustomerId { get; init; }
    public MerchantId? MerchantId { get; init; }
    public PaymentStatus? Status { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Currency { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Pagination parameters.
/// </summary>
public record PaginationParams
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int MaxPageSize { get; init; } = 100;
}

/// <summary>
/// Payment sorting options.
/// </summary>
public record PaymentSortOptions
{
    public PaymentSortField SortBy { get; init; } = PaymentSortField.InitiatedAt;
    public SortDirection Direction { get; init; } = SortDirection.Descending;
}

public enum PaymentSortField
{
    InitiatedAt,
    Amount,
    Status,
    CustomerId,
    MerchantId,
    LastUpdatedAt
}

public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Payment summary for search results.
/// </summary>
public record PaymentSummary
{
    public required PaymentId PaymentId { get; init; }
    public required PaymentStatus Status { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required CustomerId CustomerId { get; init; }
    public required MerchantId MerchantId { get; init; }
    public required DateTimeOffset InitiatedAt { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Pagination metadata.
/// </summary>
public record PaginationMetadata
{
    public required int CurrentPage { get; init; }
    public required int PageSize { get; init; }
    public required int TotalItems { get; init; }
    public required int TotalPages { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
}

/// <summary>
/// Search execution metadata.
/// </summary>
public record SearchMetadata
{
    public required TimeSpan ExecutionTime { get; init; }
    public required int ResultCount { get; init; }
    public bool IsFromCache { get; init; }
    public string? IndexesUsed { get; init; }
}

/// <summary>
/// Payment statistics grouping options.
/// </summary>
public enum PaymentStatsGrouping
{
    Hourly,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Payment metrics options.
/// </summary>
[Flags]
public enum PaymentMetrics
{
    None = 0,
    Count = 1,
    Amount = 2,
    AverageAmount = 4,
    StatusDistribution = 8,
    PaymentMethods = 16,
    Currencies = 32,
    All = Count | Amount | AverageAmount | StatusDistribution | PaymentMethods | Currencies
}

/// <summary>
/// Payment statistics summary.
/// </summary>
public record PaymentStatsSummary
{
    public int TotalPayments { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AverageAmount { get; init; }
    public int SuccessfulPayments { get; init; }
    public int FailedPayments { get; init; }
    public decimal SuccessRate { get; init; }
    public string? TopPaymentMethod { get; init; }
    public string? TopCurrency { get; init; }
}

/// <summary>
/// Grouped payment statistics.
/// </summary>
public record PaymentStatsGroup
{
    public required DateTimeOffset PeriodStart { get; init; }
    public required DateTimeOffset PeriodEnd { get; init; }
    public required PaymentStatsSummary Stats { get; init; }
}

/// <summary>
/// Payment method statistics.
/// </summary>
public record PaymentMethodStats
{
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AverageAmount { get; init; }
    public decimal SuccessRate { get; init; }
}

/// <summary>
/// Currency statistics.
/// </summary>
public record CurrencyStats
{
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AverageAmount { get; init; }
}