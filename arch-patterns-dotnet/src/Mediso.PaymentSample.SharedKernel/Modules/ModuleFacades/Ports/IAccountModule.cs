using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;

/// <summary>
/// Account module facade interface for cross-module communication
/// </summary>
public interface IAccountModule
{
    Task<AccountBalanceInfo> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<ReservationResult> ReserveAsync(Guid accountId, decimal amount, string currency, Guid? paymentId = null, CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<bool> HasSufficientFundsAsync(Guid accountId, decimal amount, string currency, CancellationToken cancellationToken = default);
    Task DebitAsync(Guid accountId, decimal amount, string currency, string reference, CancellationToken cancellationToken = default);
    Task CreditAsync(Guid accountId, decimal amount, string currency, string reference, CancellationToken cancellationToken = default);
}