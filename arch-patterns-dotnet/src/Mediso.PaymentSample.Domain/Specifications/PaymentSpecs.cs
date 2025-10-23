using Mediso.PaymentSample.Domain.Payments;

namespace Mediso.PaymentSample.Domain.Specifications;

public static class PaymentSpecs
{
    public static bool CanBeCancelled(this Payment p)
        => p.State is PaymentState.Requested or PaymentState.Flagged or PaymentState.Released;
}