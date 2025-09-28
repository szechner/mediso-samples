using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Ledger.Module;

/// <summary>
/// Public interface for the Ledger module - defines the contract for external modules
/// </summary>
public interface ILedgerModule
{
    Task<JournalResult> CreateJournalEntriesAsync(CreateJournalCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntry>> GetEntriesAsync(PaymentId paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntry>> GetAccountEntriesAsync(AccountId accountId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default);
    Task<bool> ValidateBalanceAsync(AccountId accountId, CancellationToken cancellationToken = default);
    Task<AccountLedgerBalance> GetLedgerBalanceAsync(AccountId accountId, CancellationToken cancellationToken = default);
    Task ReverseJournalAsync(PaymentId paymentId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command for creating journal entries - module boundary contract
/// </summary>
public sealed record CreateJournalCommand(
    PaymentId PaymentId,
    AccountId DebitAccount,
    AccountId CreditAccount,
    Money Amount,
    string Reference
);

/// <summary>
/// Result of journal creation operation
/// </summary>
public sealed record JournalResult(
    bool Success,
    IReadOnlyList<JournalEntry> Entries,
    string? FailureReason = null
);

/// <summary>
/// Journal entry for external consumption - module boundary contract
/// </summary>
public sealed record JournalEntry(
    LedgerEntryId EntryId,
    PaymentId PaymentId,
    AccountId DebitAccount,
    AccountId CreditAccount,
    Money Amount,
    string Reference,
    DateTimeOffset CreatedAt,
    JournalEntryStatus Status = JournalEntryStatus.Posted
);

/// <summary>
/// Account balance from ledger perspective
/// </summary>
public sealed record AccountLedgerBalance(
    AccountId AccountId,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal NetBalance,
    DateTimeOffset CalculatedAt
)
{
    /// <summary>
    /// Net balance (Credits - Debits)
    /// </summary>
    public decimal NetBalance { get; } = CreditTotal - DebitTotal;
}

/// <summary>
/// Status of journal entry
/// </summary>
public enum JournalEntryStatus
{
    Pending,
    Posted,
    Reversed,
    Failed
}