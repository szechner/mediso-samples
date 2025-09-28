using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Common;

/// <summary>
/// PaymentId using readonly record struct for optimal performance
/// </summary>
public readonly record struct PaymentId
{
    private readonly Guid _value;
    
    /// <summary>
    /// Creates a new PaymentId with validation
    /// </summary>
    /// <param name="value">GUID value for the payment ID</param>
    /// <exception cref="DomainException">Thrown when value is empty</exception>
    public PaymentId(Guid value) 
    {
        _value = value != Guid.Empty ? value : 
            throw new DomainException("PaymentId cannot be empty");
    }
    
    /// <summary>
    /// Gets the underlying GUID value
    /// </summary>
    public Guid Value => _value;
    
    /// <summary>
    /// Creates a new PaymentId with a generated GUID
    /// </summary>
    public static PaymentId New() => new(Guid.NewGuid());
    
    /// <summary>
    /// Returns string representation of the ID
    /// </summary>
    public override string ToString() => _value.ToString();
    
    /// <summary>
    /// Implicit conversion to Guid for compatibility
    /// </summary>
    public static implicit operator Guid(PaymentId id) => id._value;
    
    /// <summary>
    /// Creates PaymentId from Guid
    /// </summary>
    public static implicit operator PaymentId(Guid value) => new(value);
}

/// <summary>
/// ReservationId using readonly record struct for optimal performance
/// </summary>
public readonly record struct ReservationId
{
    private readonly Guid _value;
    
    /// <summary>
    /// Creates a new ReservationId with validation
    /// </summary>
    /// <param name="value">GUID value for the reservation ID</param>
    /// <exception cref="DomainException">Thrown when value is empty</exception>
    public ReservationId(Guid value) 
    {
        _value = value != Guid.Empty ? value : 
            throw new DomainException("ReservationId cannot be empty");
    }
    
    /// <summary>
    /// Gets the underlying GUID value
    /// </summary>
    public Guid Value => _value;
    
    /// <summary>
    /// Creates a new ReservationId with a generated GUID
    /// </summary>
    public static ReservationId New() => new(Guid.NewGuid());
    
    /// <summary>
    /// Returns string representation of the ID
    /// </summary>
    public override string ToString() => _value.ToString();
    
    /// <summary>
    /// Implicit conversion to Guid for compatibility
    /// </summary>
    public static implicit operator Guid(ReservationId id) => id._value;
    
    /// <summary>
    /// Creates ReservationId from Guid
    /// </summary>
    public static implicit operator ReservationId(Guid value) => new(value);
}

/// <summary>
/// LedgerEntryId using readonly record struct for optimal performance
/// </summary>
public readonly record struct LedgerEntryId
{
    private readonly Guid _value;
    
    /// <summary>
    /// Creates a new LedgerEntryId with validation
    /// </summary>
    /// <param name="value">GUID value for the ledger entry ID</param>
    /// <exception cref="DomainException">Thrown when value is empty</exception>
    public LedgerEntryId(Guid value) 
    {
        _value = value != Guid.Empty ? value : 
            throw new DomainException("LedgerEntryId cannot be empty");
    }
    
    /// <summary>
    /// Gets the underlying GUID value
    /// </summary>
    public Guid Value => _value;
    
    /// <summary>
    /// Creates a new LedgerEntryId with a generated GUID
    /// </summary>
    public static LedgerEntryId New() => new(Guid.NewGuid());
    
    /// <summary>
    /// Returns string representation of the ID
    /// </summary>
    public override string ToString() => _value.ToString();
    
    /// <summary>
    /// Implicit conversion to Guid for compatibility
    /// </summary>
    public static implicit operator Guid(LedgerEntryId id) => id._value;
    
    /// <summary>
    /// Creates LedgerEntryId from Guid
    /// </summary>
    public static implicit operator LedgerEntryId(Guid value) => new(value);
}

/// <summary>
/// AccountId using readonly record struct with string validation for optimal performance
/// </summary>
public readonly record struct AccountId
{
    private readonly string _value;
    
    /// <summary>
    /// Creates a new AccountId with validation
    /// </summary>
    /// <param name="value">String value for the account ID</param>
    /// <exception cref="DomainException">Thrown when value is null, empty, or invalid</exception>
    public AccountId(string value) 
    {
        if (IsValidAccountId(value.AsSpan()))
            _value = value;
        else
            throw new DomainException("AccountId cannot be empty or invalid");
    }
    
    /// <summary>
    /// Gets the underlying string value
    /// </summary>
    public string Value => _value ?? string.Empty;
    
    /// <summary>
    /// Creates AccountId from string value
    /// </summary>
    public static AccountId New(string id) => new(id);
    
    /// <summary>
    /// Returns string representation of the ID
    /// </summary>
    public override string ToString() => _value ?? string.Empty;
    
    /// <summary>
    /// Implicit conversion to string for compatibility
    /// </summary>
    public static implicit operator string(AccountId id) => id._value ?? string.Empty;
    
    /// <summary>
    /// Creates AccountId from string
    /// </summary>
    public static implicit operator AccountId(string value) => new(value);
    
    /// <summary>
    /// Validates account ID using Span<char> for performance
    /// </summary>
    /// <param name="value">Account ID value to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidAccountId(ReadOnlySpan<char> value)
    {
        return !value.IsEmpty && 
               !value.IsWhiteSpace() && 
               value.Length <= 50 && // Reasonable upper limit
               value.Length >= 1;    // Minimum length
    }
}
