namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record RiskProfileInfo(
    Guid AccountId,
    string RiskLevel,
    bool IsHighRisk,
    DateTimeOffset LastUpdated
);