using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;

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