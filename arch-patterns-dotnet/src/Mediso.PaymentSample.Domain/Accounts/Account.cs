using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;
using System.Runtime.CompilerServices;

namespace Mediso.PaymentSample.Domain.Accounts;

/// <summary>
/// Account class with caching for improved performance
/// </summary>
public sealed class Account
{
    private decimal _availableBalance;
    private bool _availableBalanceValid;
    
    /// <summary>
    /// Account identifier
    /// </summary>
    public AccountId Id { get; }
    
    /// <summary>
    /// Account currency
    /// </summary>
    public Currency Currency { get; }
    
    /// <summary>
    /// Total account balance
    /// </summary>
    public decimal Balance { get; private set; }
    
    /// <summary>
    /// Amount reserved (held) in the account
    /// </summary>
    public decimal Reserved { get; private set; }

    /// <summary>
    /// Available balance for new transactions (cached)
    /// </summary>
    public decimal AvailableBalance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!_availableBalanceValid)
            {
                _availableBalance = Balance - Reserved;
                _availableBalanceValid = true;
            }
            return _availableBalance;
        }
    }

    /// <summary>
    /// Creates a new account
    /// </summary>
    /// <param name="id">Account identifier</param>
    /// <param name="currency">Account currency</param>
    /// <param name="balance">Initial balance</param>
    /// <param name="reserved">Initial reserved amount</param>
    public Account(AccountId id, Currency currency, decimal balance = 0m, decimal reserved = 0m)
    {
        Id = id;
        Currency = currency;
        Balance = balance;
        Reserved = reserved;
        _availableBalanceValid = false;
    }

    /// <summary>
    /// Applies a hold (reservation) on funds
    /// </summary>
    /// <param name="amount">Amount to hold</param>
    /// <exception cref="DomainException">Thrown for currency mismatch or insufficient funds</exception>
    public void ApplyHold(Money amount)
    {
        ValidateCurrency(amount.Currency);
        
        // Use cached available balance for better performance
        if (AvailableBalance < amount.Amount)
            ThrowInsufficientFunds();
            
        Reserved += amount.Amount;
        InvalidateAvailableBalanceCache();
    }

    /// <summary>
    /// Releases a hold (reservation) on funds
    /// </summary>
    /// <param name="amount">Amount to release</param>
    /// <exception cref="DomainException">Thrown for currency mismatch</exception>
    public void ReleaseHold(Money amount)
    {
        ValidateCurrency(amount.Currency);
        
        Reserved = Math.Max(0m, Reserved - amount.Amount);
        InvalidateAvailableBalanceCache();
    }

    /// <summary>
    /// Debits (subtracts) amount from account
    /// </summary>
    /// <param name="amount">Amount to debit</param>
    /// <exception cref="DomainException">Thrown for currency mismatch or insufficient funds</exception>
    public void Debit(Money amount)
    {
        ValidateCurrency(amount.Currency);
        
        if (Balance < amount.Amount)
            ThrowInsufficientFunds();
            
        Balance -= amount.Amount;
        InvalidateAvailableBalanceCache();
    }

    /// <summary>
    /// Credits (adds) amount to account
    /// </summary>
    /// <param name="amount">Amount to credit</param>
    /// <exception cref="DomainException">Thrown for currency mismatch</exception>
    public void Credit(Money amount)
    {
        ValidateCurrency(amount.Currency);
        
        Balance += amount.Amount;
        InvalidateAvailableBalanceCache();
    }
    
    /// <summary>
    /// Static method to check if account has sufficient funds without creating exceptions
    /// </summary>
    /// <param name="balance">Account balance</param>
    /// <param name="reserved">Reserved amount</param>
    /// <param name="amount">Amount to check</param>
    /// <returns>True if sufficient funds available</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSufficientFunds(decimal balance, decimal reserved, decimal amount)
        => balance - reserved >= amount;

    /// <summary>
    /// Validates currency match
    /// </summary>
    /// <param name="currency">Currency to validate</param>
    /// <exception cref="DomainException">Thrown for currency mismatch</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCurrency(Currency currency)
    {
        if (currency.Code != Currency.Code)
            ThrowCurrencyMismatch();
    }
    
    /// <summary>
    /// Invalidates the available balance cache
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidateAvailableBalanceCache()
    {
        _availableBalanceValid = false;
    }
    
    /// <summary>
    /// Throws currency mismatch exception (out-of-line for better performance)
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCurrencyMismatch()
    {
        throw new DomainException("Currency mismatch");
    }
    
    /// <summary>
    /// Throws insufficient funds exception (out-of-line for better performance)
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInsufficientFunds()
    {
        throw new DomainException("Insufficient funds");
    }
}
