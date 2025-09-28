using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Common;

public sealed record PaymentId : NonEmptyGuid
{
    public PaymentId(Guid value) : base(value) { }
    public static PaymentId New() => new(Guid.NewGuid());
}


public sealed record ReservationId : NonEmptyGuid
{
    public ReservationId(Guid value) : base(value) { }
    public static ReservationId New() => new(Guid.NewGuid());
}


public sealed record LedgerEntryId : NonEmptyGuid
{
    public LedgerEntryId(Guid value) : base(value) { }
    public static LedgerEntryId New() => new(Guid.NewGuid());
}


public sealed record AccountId : NonEmptyString
{
    public AccountId(string value) : base(value) { }
    public static AccountId New(string id) => new(id);
}