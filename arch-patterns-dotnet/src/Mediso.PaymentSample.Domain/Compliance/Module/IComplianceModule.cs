using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Compliance.Module;

/// <summary>
/// Public interface for the Compliance module - defines the contract for external modules
/// </summary>
public interface IComplianceModule
{
    Task<AMLScreeningResult> ScreenPaymentAsync(ScreenPaymentCommand command, CancellationToken cancellationToken = default);
    Task<ComplianceDecision> ReviewFlaggedPaymentAsync(PaymentId paymentId, ReviewDecisionCommand decision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlaggedPayment>> GetPendingReviewsAsync(CancellationToken cancellationToken = default);
    Task<ComplianceProfile> GetProfileAsync(AccountId accountId, CancellationToken cancellationToken = default);
    Task UpdateRiskProfileAsync(AccountId accountId, RiskProfile riskProfile, CancellationToken cancellationToken = default);
    Task<bool> IsWithinLimitsAsync(AccountId accountId, Money amount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command for AML screening - module boundary contract
/// </summary>
public sealed record ScreenPaymentCommand(
    PaymentId PaymentId,
    AccountId PayerAccountId,
    AccountId PayeeAccountId,
    Money Amount,
    string Reference
);

/// <summary>
/// Result of AML screening operation
/// </summary>
public sealed record AMLScreeningResult(
    PaymentId PaymentId,
    bool Passed,
    string RuleSetVersion,
    IReadOnlyList<ComplianceFlag> Flags,
    RiskScore RiskScore
);

/// <summary>
/// Command for manual review decision
/// </summary>
public sealed record ReviewDecisionCommand(
    bool Approved,
    string ReviewedBy,
    string? Reason = null,
    string? Notes = null
);

/// <summary>
/// Compliance decision result
/// </summary>
public sealed record ComplianceDecision(
    PaymentId PaymentId,
    bool Approved,
    string ReviewedBy,
    DateTimeOffset ReviewedAt,
    string? Reason = null
);

/// <summary>
/// Flagged payment awaiting manual review
/// </summary>
public sealed record FlaggedPayment(
    PaymentId PaymentId,
    Money Amount,
    AccountId PayerAccountId,
    AccountId PayeeAccountId,
    IReadOnlyList<ComplianceFlag> Flags,
    RiskScore RiskScore,
    DateTimeOffset FlaggedAt
);

/// <summary>
/// Compliance flag details
/// </summary>
public sealed record ComplianceFlag(
    string RuleCode,
    string Description,
    ComplianceFlagSeverity Severity,
    string? Details = null
);

/// <summary>
/// Risk score calculation
/// </summary>
public sealed record RiskScore(
    decimal Score,
    RiskLevel Level,
    string CalculationMethod = "v1.0"
)
{
    public RiskLevel Level { get; } = Score switch
    {
        <= 0.3m => RiskLevel.Low,
        <= 0.7m => RiskLevel.Medium,
        <= 0.9m => RiskLevel.High,
        _ => RiskLevel.Critical
    };
}

/// <summary>
/// Compliance profile for an account
/// </summary>
public sealed record ComplianceProfile(
    AccountId AccountId,
    RiskProfile RiskProfile,
    IReadOnlyList<ComplianceLimit> Limits,
    DateTimeOffset LastUpdated
);

/// <summary>
/// Risk profile information
/// </summary>
public sealed record RiskProfile(
    RiskLevel BaseRiskLevel,
    string CountryCode,
    bool IsHighRiskCountry,
    bool IsPEP,
    DateTimeOffset LastKYCUpdate
);

/// <summary>
/// Compliance limits for an account
/// </summary>
public sealed record ComplianceLimit(
    string LimitType,
    Money Amount,
    TimeSpan Period,
    Money Used,
    DateTimeOffset ResetAt
);

/// <summary>
/// Risk level enumeration
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Compliance flag severity
/// </summary>
public enum ComplianceFlagSeverity
{
    Info,
    Warning,
    Critical,
    BlockTransaction
}