using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain;

// Hexagonal ports (domain-facing interfaces) – implementace v Infrastructure


public interface IFundsReservationService
{
    Task<ReservationId> ReserveAsync(AccountId accountId, Money amount, CancellationToken ct);
    Task ReleaseAsync(ReservationId reservationId, CancellationToken ct);
}


public interface ILedgerService
{
    Task<IReadOnlyList<Payments.LedgerEntry>> JournalAsync(
        PaymentId paymentId,
        AccountId debit,
        AccountId credit,
        Money amount,
        CancellationToken ct);
}


public interface ISettlementGateway
{
    Task<(bool success, string? externalRef, string? reason)> SettleAsync(PaymentId paymentId, Money amount, CancellationToken ct);
}


public interface INotificationPublisher
{
    Task PublishAsync(PaymentId paymentId, string channel, CancellationToken ct);
}


public interface IPaymentEventStore
{
// Persistence boundary for ES aggregate
    Task AppendAsync(Payments.Payment payment, CancellationToken ct);
    Task<Payments.Payment?> LoadAsync(PaymentId id, CancellationToken ct);
}