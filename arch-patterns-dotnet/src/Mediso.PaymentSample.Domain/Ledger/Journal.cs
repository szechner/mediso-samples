using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Mediso.PaymentSample.Domain.Ledger;

/// <summary>
/// Journal class using ArrayPool for efficient memory management
/// </summary>
public sealed class Journal : IDisposable
{
    private static readonly ArrayPool<JournalEntry> _entryPool = ArrayPool<JournalEntry>.Shared;
    private JournalEntry[]? _rentedArray;
    private int _count;
    private bool _disposed;

    /// <summary>
    /// Gets the journal entries as a ReadOnlySpan
    /// </summary>
    public ReadOnlySpan<JournalEntry> Entries => 
        _rentedArray == null ? ReadOnlySpan<JournalEntry>.Empty : 
        new ReadOnlySpan<JournalEntry>(_rentedArray, 0, _count);

    /// <summary>
    /// Gets the number of entries in the journal
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds a journal entry with validation
    /// </summary>
    /// <param name="debit">Debit account ID</param>
    /// <param name="credit">Credit account ID</param>
    /// <param name="amount">Transaction amount</param>
    /// <exception cref="DomainException">Thrown when debit and credit are the same account</exception>
    /// <exception cref="ObjectDisposedException">Thrown when journal has been disposed</exception>
    public void AddEntry(AccountId debit, AccountId credit, Money amount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (debit.Value == credit.Value) 
            throw new DomainException("Debit and credit cannot be same account");
            
        EnsureCapacity();
        
        _rentedArray![_count++] = new JournalEntry(
            LedgerEntryId.New(), debit, credit, amount);
    }

    /// <summary>
    /// Checks if the journal is balanced (simplified implementation)
    /// </summary>
    /// <returns>True if journal has entries</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBalanced()
    {
        return _count > 0; // Pro demonstraci ponecháno jednoduché; v praxi sčítat debet/kredit zvlášť
    }

    /// <summary>
    /// Creates a copy of entries as an array (for compatibility with existing APIs)
    /// </summary>
    /// <returns>Array copy of journal entries</returns>
    public JournalEntry[] ToArray()
    {
        if (_count == 0) return Array.Empty<JournalEntry>();
        
        var result = new JournalEntry[_count];
        Entries.CopyTo(result);
        return result;
    }

    /// <summary>
    /// Ensures sufficient capacity for new entries
    /// </summary>
    private void EnsureCapacity()
    {
        if (_rentedArray == null)
        {
            // Most journals have 2-4 entries (debit/credit pairs)
            _rentedArray = _entryPool.Rent(4);
        }
        else if (_count == _rentedArray.Length)
        {
            // Grow the array when full
            var oldArray = _rentedArray;
            var newSize = Math.Max(4, _count * 2);
            _rentedArray = _entryPool.Rent(newSize);
            
            // Copy existing entries
            Array.Copy(oldArray, _rentedArray, _count);
            _entryPool.Return(oldArray, clearArray: true);
        }
    }

    /// <summary>
    /// Disposes the journal and returns rented arrays to the pool
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        if (_rentedArray != null)
        {
            _entryPool.Return(_rentedArray, clearArray: true);
            _rentedArray = null;
        }
        
        _disposed = true;
    }
}

/// <summary>
/// Journal entry record
/// </summary>
public sealed record JournalEntry(LedgerEntryId Id, AccountId DebitAccountId, AccountId CreditAccountId, Money Amount);
