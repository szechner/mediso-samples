using Mediso.PaymentSample.SharedKernel.Attributes;
using Microsoft.AspNetCore.Http;

namespace Mediso.PaymentSample.SharedKernel.Modules;

/// <summary>
/// Payment module facade interface for cross-module communication
/// Defines the public contract without domain dependencies
/// </summary>
public interface IPaymentModule
{
    Task<IResult> CreatePaymentAsync(CreatePaymentRequest request, string caller, CancellationToken cancellationToken = default);
    Task<PaymentInfo?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task ProcessComplianceResultAsync(Guid paymentId, ComplianceResult result, CancellationToken cancellationToken = default);
    Task ProcessReservationResultAsync(Guid paymentId, ReservationResult result, CancellationToken cancellationToken = default);
    Task CancelPaymentAsync(Guid paymentId, string cancelledBy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Account module facade interface for cross-module communication
/// </summary>
public interface IAccountModule
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
public interface IComplianceModule
{
    Task<ComplianceResult> ScreenPaymentAsync(ScreenPaymentRequest request, CancellationToken cancellationToken = default);
    Task<ComplianceDecisionResult> ReviewFlaggedPaymentAsync(Guid paymentId, ReviewDecisionRequest decision, CancellationToken cancellationToken = default);
    Task<bool> IsWithinLimitsAsync(Guid accountId, decimal amount, string currency, CancellationToken cancellationToken = default);
    Task<RiskProfileInfo> GetRiskProfileAsync(Guid accountId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ledger module facade interface for cross-module communication
/// </summary>
public interface ILedgerModule
{
    Task<JournalResult> CreateJournalEntriesAsync(CreateJournalRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntryInfo>> GetEntriesAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<AccountLedgerBalanceInfo> GetLedgerBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<bool> ValidateBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
}

// ============ FACADE DTOs ============

/// <summary>
/// Request to create a new payment using saga orchestration.
/// Enhanced to support fraud detection and comprehensive payment processing.
/// </summary>
[RequireModuleAccess("Payments", "CreatePayment")]
public record CreatePaymentRequest
{
    /// <summary>Payment amount in the smallest currency unit.</summary>
    public decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code (e.g., USD, EUR, CZK).</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Payer account identifier (customer ID).</summary>
    public string PayerAccountId { get; init; } = string.Empty;

    /// <summary>Payee account identifier (merchant ID).</summary>
    public string PayeeAccountId { get; init; } = string.Empty;

    /// <summary>Payment reference or description.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Payment method (credit-card, bank-transfer, etc.).</summary>
    public string? PaymentMethod { get; init; }

    /// <summary>Idempotency key for duplicate request handling.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Additional metadata for processing context.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Response from payment creation with saga orchestration status.
/// Provides comprehensive information about the initiated payment process.
/// </summary>
public record PaymentResponse
{
    /// <summary>Generated payment identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Payment amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Payment currency.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Payer account identifier.</summary>
    public string PayerAccountId { get; init; } = string.Empty;

    /// <summary>Payee account identifier.</summary>
    public string PayeeAccountId { get; init; } = string.Empty;

    /// <summary>Payment reference.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Current payment state (domain aggregate state).</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>Payment creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Processing status (saga orchestration status).</summary>
    public string? ProcessingStatus { get; init; }

    /// <summary>Correlation ID for distributed tracing.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Idempotency key used.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Estimated completion time (if applicable).</summary>
    public DateTimeOffset? EstimatedCompletionAt { get; init; }

    /// <summary>Next steps in the payment process.</summary>
    public List<string>? NextSteps { get; init; }
}

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
