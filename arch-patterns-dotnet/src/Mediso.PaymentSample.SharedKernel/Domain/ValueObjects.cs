namespace Mediso.PaymentSample.SharedKernel.Domain;

/// <summary>
/// Currency value object using readonly record struct
/// </summary>
public readonly record struct Currency(string Code)
{
    public override string ToString() => Code;
}

/// <summary>
/// Money value object using readonly record struct
/// </summary>
public readonly record struct Money(decimal Amount, Currency Currency)
{
    /// <summary>
    /// Ensures the money amount is positive
    /// </summary>
    /// <returns>The money instance if positive</returns>
    /// <exception cref="DomainException">Thrown when amount is not positive</exception>
    public Money EnsurePositive()
        => Amount <= 0 ? throw new DomainException("Amount must be > 0") : this;
}
