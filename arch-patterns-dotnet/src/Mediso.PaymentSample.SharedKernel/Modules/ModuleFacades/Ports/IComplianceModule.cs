using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;

/// <summary>
/// Compliance module facade interface for cross-module communication
/// </summary>
public interface IComplianceModule
{
    Task<ComplianceResult> ScreenPaymentAsync(ScreenPaymentRequest request, CancellationToken cancellationToken = default);
    Task<ComplianceDecisionResult> ReviewFlaggedPaymentAsync(Guid paymentId, ReviewDecisionRequest decision, CancellationToken cancellationToken = default);
    Task<bool> IsWithinLimitsAsync(Guid accountId, decimal amount, string currency, CancellationToken cancellationToken = default);
    Task<RiskProfileInfo> GetRiskProfileAsync(Guid accountId, CancellationToken cancellationToken = default);
}