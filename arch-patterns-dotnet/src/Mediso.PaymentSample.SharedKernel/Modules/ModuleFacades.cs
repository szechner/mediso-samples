namespace Mediso.PaymentSample.SharedKernel.Modules;

/// <summary>
/// Payment module facade interface for cross-module communication
/// Defines the public contract without domain dependencies
/// </summary>
public interface IPaymentModuleFacade
{
    Task<Guid> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentInfo?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task ProcessComplianceResultAsync(Guid paymentId, ComplianceResult result, CancellationToken cancellationToken = default);
    Task ProcessReservationResultAsync(Guid paymentId, ReservationResult result, CancellationToken cancellationToken = default);
    Task CancelPaymentAsync(Guid paymentId, string cancelledBy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Account module facade interface for cross-module communication
/// </summary>
public interface IAccountModuleFacade
{
    Task<AccountBalanceInfo> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<ReservationResult> ReserveAsync(Guid accountId, decimal amount, string currency, Guid? paymentId = null, CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<bool> HasSufficientFundsAsync(Guid accountId, decimal amount, string currency, CancellationToken cancellationToken = default);
    Task DebitAsync(Guid accountId, decimal amount, string currency, string reference, CancellationToken cancellationToken = default);
    Task CreditAsync(Guid accountId, decimal amount, string currency, string reference, CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance module facade interface for cross-module communication
/// </summary>
public interface IComplianceModuleFacade
{
    Task<ComplianceResult> ScreenPaymentAsync(ScreenPaymentRequest request, CancellationToken cancellationToken = default);
    Task<ComplianceDecisionResult> ReviewFlaggedPaymentAsync(Guid paymentId, ReviewDecisionRequest decision, CancellationToken cancellationToken = default);
    Task<bool> IsWithinLimitsAsync(Guid accountId, decimal amount, string currency, CancellationToken cancellationToken = default);
    Task<RiskProfileInfo> GetRiskProfileAsync(Guid accountId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ledger module facade interface for cross-module communication
/// </summary>
public interface ILedgerModuleFacade
{
    Task<JournalResult> CreateJournalEntriesAsync(CreateJournalRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntryInfo>> GetEntriesAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<AccountLedgerBalanceInfo> GetLedgerBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<bool> ValidateBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
}

// ============ FACADE DTOs ============

public sealed record CreatePaymentRequest(
    decimal Amount,
    string Currency,
    Guid PayerAccountId,
    Guid PayeeAccountId,
    string Reference
);

public sealed record PaymentInfo(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    Guid PayerAccountId,
    Guid PayeeAccountId,
    string Reference,
    string Status,
    DateTimeOffset CreatedAt
);

public sealed record ComplianceResult(
    bool Passed,
    string? Reason = null,
    string? RiskScore = null,
    string[]? Flags = null
);

public sealed record AccountBalanceInfo(
    Guid AccountId,
    string Currency,
    decimal Available,
    decimal Reserved,
    decimal Total
);

public sealed record ReservationResult(
    bool Success,
    Guid? ReservationId = null,
    string? FailureReason = null
);

public sealed record ScreenPaymentRequest(
    Guid PaymentId,
    Guid PayerAccountId,
    Guid PayeeAccountId,
    decimal Amount,
    string Currency,
    string Reference
);

public sealed record ComplianceDecisionResult(
    Guid PaymentId,
    bool Approved,
    string ReviewedBy,
    DateTimeOffset ReviewedAt,
    string? Reason = null
);

public sealed record ReviewDecisionRequest(
    bool Approved,
    string ReviewedBy,
    string? Reason = null
);

public sealed record RiskProfileInfo(
    Guid AccountId,
    string RiskLevel,
    bool IsHighRisk,
    DateTimeOffset LastUpdated
);

public sealed record CreateJournalRequest(
    Guid PaymentId,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Currency,
    string Reference
);

public sealed record JournalResult(
    bool Success,
    Guid? JournalId = null,
    string? FailureReason = null
);

public sealed record JournalEntryInfo(
    Guid EntryId,
    Guid PaymentId,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Currency,
    string Reference,
    DateTimeOffset CreatedAt
);

public sealed record AccountLedgerBalanceInfo(
    Guid AccountId,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal NetBalance,
    DateTimeOffset CalculatedAt
);
