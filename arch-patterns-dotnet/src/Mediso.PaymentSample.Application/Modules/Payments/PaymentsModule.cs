using Mediso.PaymentSample.Application.Common.Extensions;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Modules;
using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;
using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;
using Wolverine;

namespace Mediso.PaymentSample.Application.Modules.Payments;

public class PaymentsModule : IPaymentModule
{
    private readonly IMessageBus _bus;
    
    public static string Name => "Payments";
    
    
    public PaymentsModule(IMessageBus bus)
    {
        _bus = bus;
    }
    
    public Task<SharedPaymentResult> CreatePaymentAsync(CreatePaymentRequest request, string caller, CancellationToken cancellationToken = default)
    {
        return _bus.InvokeAsync<SharedPaymentResult>(request.WithDeliveryOptions(MessagingExtensions.CreateDeliveryOptions(caller)), cancellationToken);
    }

    public Task<PaymentResponse?> GetPaymentAsync(Guid paymentId, string caller, CancellationToken cancellationToken = default)
    {
        var query = new GetPaymentQuery()
        {
            PaymentId = new PaymentId(paymentId)
        };
        return _bus.InvokeAsync<PaymentResponse?>(query, cancellationToken);
    }

    public Task<PaymentStatusResponse?> GetPaymentStatusAsync(string correlationId, string caller, CancellationToken cancellationToken = default)
    {
        var query = new GetPaymentStatusQuery()
        {
            CorrelationId = correlationId
        };
        return _bus.InvokeAsync<PaymentStatusResponse?>(query, cancellationToken);
    }

    public Task ProcessComplianceResultAsync(Guid paymentId, ComplianceResult result, string caller, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task ProcessReservationResultAsync(Guid paymentId, ReservationResult result, string caller, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CancelPaymentAsync(Guid paymentId, string cancelledBy, string caller, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}