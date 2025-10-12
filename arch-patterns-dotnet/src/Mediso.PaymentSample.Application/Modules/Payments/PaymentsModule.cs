using Mediso.PaymentSample.SharedKernel.Modules;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Mediso.PaymentSample.Application.Modules.Payments;

public class PaymentsModule : IPaymentModule
{
    private readonly IMessageBus _bus;
    private static DeliveryOptions CallerOptions(string caller)
    {
        var options = new DeliveryOptions();
        options.Headers["X-Caller-Module"] = caller;

        return options;
    }
    
    public PaymentsModule(IMessageBus bus)
    {
        _bus = bus;
    }
    
    public Task<IResult> CreatePaymentAsync(CreatePaymentRequest request, string caller, CancellationToken cancellationToken = default)
    {
        return _bus.InvokeAsync<IResult>(request.WithDeliveryOptions(CallerOptions(caller)), cancellationToken);
    }

    public Task<PaymentInfo?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task ProcessComplianceResultAsync(Guid paymentId, ComplianceResult result, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task ProcessReservationResultAsync(Guid paymentId, ReservationResult result, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CancelPaymentAsync(Guid paymentId, string cancelledBy, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}