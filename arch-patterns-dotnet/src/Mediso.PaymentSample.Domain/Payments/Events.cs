using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Payments;

public sealed record PaymentRequested(
    PaymentId PaymentId,
    Money Amount,
    AccountId PayerAccountId,
    AccountId PayeeAccountId,
    string Reference
) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record AMLPassed(PaymentId PaymentId, string RuleSetVersion) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record PaymentFlagged(PaymentId PaymentId, string Reason, string Severity) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record FundsReserved(PaymentId PaymentId, ReservationId ReservationId, Money Amount) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record FundsReservationFailed(PaymentId PaymentId, string Reason) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record PaymentJournaled(PaymentId PaymentId, IReadOnlyList<LedgerEntry> Entries) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record PaymentSettled(PaymentId PaymentId, string Channel, string? ExternalRef) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record PaymentCancelled(PaymentId PaymentId, string By) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record PaymentDeclined(PaymentId PaymentId, string Reason) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record PaymentFailed(PaymentId PaymentId, string Reason) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record PaymentNotified(PaymentId PaymentId, string Channel) : IDomainEvent
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
};

public sealed record LedgerEntry(LedgerEntryId EntryId, Domain.Common.AccountId DebitAccountId, Domain.Common.AccountId CreditAccountId, Money Amount);