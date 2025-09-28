using Mediso.PaymentSample.Domain.Payments;

namespace Mediso.PaymentSample.Domain.Compliance;

public interface ICompliancePolicy
{
    ComplianceResult Evaluate(Payment payment);
}


public sealed record ComplianceResult(bool Passed, string? Reason = null, string Severity = "");


public sealed class SimpleAmountThresholdPolicy(decimal threshold) : ICompliancePolicy
{
    public ComplianceResult Evaluate(Payment payment)
    {
        return payment.Amount.Amount > threshold
            ? new ComplianceResult(false, $"Amount above threshold {threshold}", "MEDIUM")
            : new ComplianceResult(true);
    }
}