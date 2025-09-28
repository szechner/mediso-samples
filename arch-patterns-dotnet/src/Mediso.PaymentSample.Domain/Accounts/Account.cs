using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Accounts;

public sealed class Account
{
    public AccountId Id { get; }
    public Currency Currency { get; }
    public decimal Balance { get; private set; }
    public decimal Reserved { get; private set; }


    public Account(AccountId id, Currency currency, decimal balance = 0m, decimal reserved = 0m)
    {
        Id = id;
        Currency = currency;
        Balance = balance;
        Reserved = reserved;
    }


    public void ApplyHold(Money amount)
    {
        if (amount.Currency != Currency) throw new DomainException("Currency mismatch");
        if (Balance - Reserved < amount.Amount) throw new DomainException("Insufficient funds");
        Reserved += amount.Amount;
    }


    public void ReleaseHold(Money amount)
    {
        if (amount.Currency != Currency) throw new DomainException("Currency mismatch");
        Reserved = Math.Max(0m, Reserved - amount.Amount);
    }


    public void Debit(Money amount)
    {
        if (amount.Currency != Currency) throw new DomainException("Currency mismatch");
        if (Balance < amount.Amount) throw new DomainException("Insufficient funds");
        Balance -= amount.Amount;
    }


    public void Credit(Money amount)
    {
        if (amount.Currency != Currency) throw new DomainException("Currency mismatch");
        Balance += amount.Amount;
    }
}