namespace Mediso.PaymentSample.SharedKernel.Domain;

public readonly record struct Currency(string Code)
{
    public override string ToString() => Code;
}

public readonly record struct Money(decimal Amount, Currency Currency)
{
    public Money EnsurePositive()
        => Amount <= 0 ? throw new DomainException("Amount must be > 0") : this;
}

public abstract record NonEmptyGuid
{
    protected NonEmptyGuid(Guid value) => Value = Guards.NotEmpty(value, nameof(value));
    public Guid Value { get; }
    public override string ToString() => Value.ToString();
    public static implicit operator Guid(NonEmptyGuid id) => id.Value;
}

public abstract record NonEmptyString
{
    protected NonEmptyString(string value) => Value = Guards.NotNullOrWhiteSpace(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value.ToString();
    public static implicit operator string(NonEmptyString id) => id.Value;
}