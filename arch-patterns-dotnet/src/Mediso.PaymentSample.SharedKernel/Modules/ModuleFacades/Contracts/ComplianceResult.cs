namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record ComplianceResult(
    bool Passed,
    string? Reason = null,
    string? RiskScore = null,
    string[]? Flags = null
);