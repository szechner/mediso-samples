using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;

/// <summary>
/// Payment module facade interface for cross-module communication
/// Defines the public contract without domain dependencies
/// </summary>
public interface IPaymentModule
{
    Task<SharedPaymentResult> CreatePaymentAsync(CreatePaymentRequest request, string caller, CancellationToken cancellationToken = default);
    Task<PaymentResponse?> GetPaymentAsync(Guid paymentId, string caller, CancellationToken cancellationToken = default);
    Task<PaymentStatusResponse?> GetPaymentStatusAsync(string correlationId, string caller, CancellationToken cancellationToken = default);
    Task ProcessComplianceResultAsync(Guid paymentId, ComplianceResult result, string caller, CancellationToken cancellationToken = default);
    Task ProcessReservationResultAsync(Guid paymentId, ReservationResult result, string caller, CancellationToken cancellationToken = default);
    Task CancelPaymentAsync(Guid paymentId, string cancelledBy, string caller, CancellationToken cancellationToken = default);
}