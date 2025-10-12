using Mediso.PaymentSample.Domain.Common;
using Wolverine.Persistence.Sagas;

namespace Mediso.PaymentSample.Application.Modules.FraudDetection.Contracts;

public sealed record RunFraudCheckCommand(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string PayerAccountId,
    string PayeeAccountId,
    string Reference
);

public sealed record FraudCheckPassed(
    Guid PaymentId,
    string RuleSetVersion,
    DateTimeOffset CheckedAt
);

public sealed record FraudCheckFlagged(
    Guid PaymentId,
    string Reason,
    string Severity,
    DateTimeOffset CheckedAt
);

/// <summary>
/// Command to perform fraud detection analysis.
/// </summary>
public record PerformFraudDetectionCommand
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    public PaymentId PaymentId { get; init; }
    public CustomerId CustomerId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public Dictionary<string, string> Metadata { get; init; } = new();
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event published when fraud detection analysis completes.
/// </summary>
public record FraudDetectionCompletedEvent
{
    /// <summary>
    /// Unique identifier for the payment processing saga instance.
    /// </summary>
    [SagaIdentity]
    public Guid PaymentProcessingSagaId { get; set; }
    
    public PaymentId PaymentId { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public decimal RiskScore { get; init; }
    public List<string> RiskFactors { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public string Provider { get; init; } = string.Empty;
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
    public Dictionary<string, string>? Metadata { get; init; }
    public FraudAnalysisDetails? AnalysisDetails { get; init; }
    public TimeSpan ProcessingDuration { get; init; }
}

/// <summary>
/// Risk level enumeration.
/// </summary>
public enum RiskLevel
{
    /// <summary>Low risk payment.</summary>
    Low,
    
    /// <summary>Medium risk payment.</summary>
    Medium,
    
    /// <summary>High risk payment.</summary>
    High,
    
    /// <summary>Blocked payment due to fraud.</summary>
    Blocked
}

/// <summary>
/// Detailed fraud analysis results for audit and machine learning.
/// </summary>
public record FraudAnalysisDetails
{
    /// <summary>Individual rule scores and results.</summary>
    public Dictionary<string, decimal> RuleScores { get; init; } = new();
    
    /// <summary>Model predictions and confidence levels.</summary>
    public Dictionary<string, decimal> ModelPredictions { get; init; } = new();
    
    /// <summary>Velocity check results.</summary>
    public VelocityCheckResults? VelocityResults { get; init; }
    
    /// <summary>Behavioral analysis results.</summary>
    public BehavioralAnalysisResults? BehavioralResults { get; init; }
    
    /// <summary>External provider responses.</summary>
    public List<ExternalProviderResponse> ExternalProviderResponses { get; init; } = new();
    
    /// <summary>Confidence level in the final decision (0.0-1.0).</summary>
    public decimal ConfidenceLevel { get; init; }
    
    /// <summary>Processing metadata for debugging.</summary>
    public Dictionary<string, object> ProcessingMetadata { get; init; } = new();
}

/// <summary>
/// Results from velocity checks (transaction frequency analysis).
/// </summary>
public record VelocityCheckResults
{
    /// <summary>Transaction count in last hour.</summary>
    public int TransactionsLastHour { get; init; }
    
    /// <summary>Transaction count in last day.</summary>
    public int TransactionsLastDay { get; init; }
    
    /// <summary>Total amount spent in last hour.</summary>
    public decimal AmountLastHour { get; init; }
    
    /// <summary>Total amount spent in last day.</summary>
    public decimal AmountLastDay { get; init; }
    
    /// <summary>Whether velocity limits were exceeded.</summary>
    public bool VelocityLimitsExceeded { get; init; }
    
    /// <summary>Specific velocity rules triggered.</summary>
    public List<string> TriggeredRules { get; init; } = new();
}

/// <summary>
/// Results from behavioral pattern analysis.
/// </summary>
public record BehavioralAnalysisResults
{
    /// <summary>How typical this transaction is for the customer (0.0-1.0).</summary>
    public decimal TypicalityScore { get; init; }
    
    /// <summary>Deviation from normal spending patterns.</summary>
    public decimal SpendingPatternDeviation { get; init; }
    
    /// <summary>Time-of-day analysis results.</summary>
    public decimal TimePatternScore { get; init; }
    
    /// <summary>Merchant category analysis results.</summary>
    public decimal MerchantCategoryScore { get; init; }
    
    /// <summary>Geographic pattern analysis results.</summary>
    public decimal GeographicPatternScore { get; init; }
    
    /// <summary>List of behavioral anomalies detected.</summary>
    public List<string> DetectedAnomalies { get; init; } = new();
}

/// <summary>
/// Response from external fraud detection provider.
/// </summary>
public record ExternalProviderResponse
{
    /// <summary>Provider name.</summary>
    public required string ProviderName { get; init; }
    
    /// <summary>Provider's risk score.</summary>
    public decimal RiskScore { get; init; }
    
    /// <summary>Provider's decision.</summary>
    public string Decision { get; init; } = string.Empty;
    
    /// <summary>Response timestamp.</summary>
    public DateTimeOffset ResponseTime { get; init; }
    
    /// <summary>Processing duration for this provider.</summary>
    public TimeSpan ProcessingDuration { get; init; }
    
    /// <summary>Whether provider was successful.</summary>
    public bool IsSuccessful { get; init; }
    
    /// <summary>Error message if provider failed.</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>Raw provider response for debugging.</summary>
    public Dictionary<string, object>? RawResponse { get; init; }
}
