using Mediso.PaymentSample.SharedKernel.Attributes;
using Mediso.PaymentSample.SharedKernel.Modules;
using Wolverine;

namespace Mediso.PaymentSample.Application.Common;

public sealed class ModuleAccessMiddleware
{
    public HandlerContinuation Before(Envelope envelope, IModuleAccessPolicy policy)
    {
        var message = envelope.Message;
        if (message is null) return HandlerContinuation.Continue;

        var isDeliveryMessage = false;
        var type = message.GetType();
        if (type.FullName.Contains("DeliveryMessage"))
        {
            isDeliveryMessage = true;
            type = type.GetGenericArguments()[0];
        }

        var attrs = type
            .GetCustomAttributes(typeof(RequireModuleAccessAttribute), inherit: true)
            .Cast<RequireModuleAccessAttribute>()
            .ToArray();

        if (attrs.Length == 0) return HandlerContinuation.Continue;

        var from = (string)null;

        if (isDeliveryMessage)
        {
            var opts = message.GetType().GetProperty("Options")?.GetValue(message) as DeliveryOptions;
            opts?.Headers?.TryGetValue("X-Caller-Module", out from);
        }
        else
        {
            envelope.Headers.TryGetValue("X-Caller-Module", out from);
        }

        if (string.IsNullOrWhiteSpace(from))
        {
            throw new ModuleAccessException("Module caller context is not set.");
        }

        foreach (var a in attrs)
        {
            policy.ValidateAccess(from, a.To, a.Operation);
        }

        return HandlerContinuation.Continue;
    }
}