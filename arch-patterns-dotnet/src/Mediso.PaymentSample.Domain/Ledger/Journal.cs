using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Ledger;

public sealed class Journal
{
    private readonly List<JournalEntry> _entries = new();
    public IReadOnlyCollection<JournalEntry> Entries => _entries.AsReadOnly();


    public void AddEntry(AccountId debit, AccountId credit, Money amount)
    {
        if (debit.Value == credit.Value) throw new DomainException("Debit and credit cannot be same account");
        _entries.Add(new JournalEntry(LedgerEntryId.New(), debit, credit, amount));
    }


    public bool IsBalanced()
    {
        return _entries.Count > 0; // Pro demonstraci ponecháno jednoduché; v praxi sčítat debet/kredit zvlášť
    }
}


public sealed record JournalEntry(LedgerEntryId Id, AccountId DebitAccountId, AccountId CreditAccountId, Money Amount);