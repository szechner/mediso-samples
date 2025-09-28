using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Payments;

public sealed record PaymentRequested(
    PaymentId PaymentId,
    Money Amount,
    AccountId PayerAccountId,
    AccountId PayeeAccountId,
    string Reference,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record AMLPassed(PaymentId PaymentId, string RuleSetVersion, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentFlagged(PaymentId PaymentId, string Reason, string Severity, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record FundsReserved(PaymentId PaymentId, ReservationId ReservationId, Money Amount, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record FundsReservationFailed(PaymentId PaymentId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentJournaled(PaymentId PaymentId, IReadOnlyList<LedgerEntry> Entries, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentSettled(PaymentId PaymentId, string Channel, string? ExternalRef, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentCancelled(PaymentId PaymentId, string By, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentDeclined(PaymentId PaymentId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentFailed(PaymentId PaymentId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentNotified(PaymentId PaymentId, string Channel, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record LedgerEntry(LedgerEntryId EntryId, Domain.Common.AccountId DebitAccountId, Domain.Common.AccountId CreditAccountId, Money Amount);