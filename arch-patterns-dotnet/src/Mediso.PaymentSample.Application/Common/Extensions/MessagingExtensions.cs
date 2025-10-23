using Wolverine;

namespace Mediso.PaymentSample.Application.Common.Extensions;

public static class MessagingExtensions
{
    public static DeliveryOptions CreateDeliveryOptions(string caller)
    {
        var options = new DeliveryOptions();
        options.Headers["X-Caller-Module"] = caller;

        return options;
    }
}