using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Accounts.Module;

/// <summary>
/// Public interface for the Accounts module - defines the contract for external modules
/// </summary>
public interface IAccountsModule
{
    Task<AccountBalance> GetBalanceAsync(AccountId accountId, CancellationToken cancellationToken = default);
    Task<ReservationResult> ReserveAsync(AccountId accountId, Money amount, PaymentId? paymentId = null, CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(ReservationId reservationId, CancellationToken cancellationToken = default);
    Task<bool> HasSufficientFundsAsync(AccountId accountId, Money amount, CancellationToken cancellationToken = default);
    Task DebitAsync(AccountId accountId, Money amount, string reference, CancellationToken cancellationToken = default);
    Task CreditAsync(AccountId accountId, Money amount, string reference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountBalance>> GetMultipleBalancesAsync(IEnumerable<AccountId> accountIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Account balance information - module boundary contract
/// </summary>
public sealed record AccountBalance(
    AccountId AccountId,
    Currency Currency,
    decimal Available,
    decimal Reserved,
    decimal Total
)
{
    /// <summary>
    /// Available balance for new transactions (Total - Reserved)
    /// </summary>
    public decimal Available { get; } = Total - Reserved;
}

/// <summary>
/// Result of funds reservation operation
/// </summary>
public sealed record ReservationResult(
    bool Success,
    ReservationId? ReservationId = null,
    string? FailureReason = null,
    AccountBalance? UpdatedBalance = null
);

/// <summary>
/// Account transaction record for audit purposes
/// </summary>
public sealed record AccountTransaction(
    AccountId AccountId,
    Money Amount,
    string Reference,
    TransactionType Type,
    DateTimeOffset ProcessedAt
);

/// <summary>
/// Type of account transaction
/// </summary>
public enum TransactionType
{
    Debit,
    Credit,
    Reserve,
    ReleaseReservation
}