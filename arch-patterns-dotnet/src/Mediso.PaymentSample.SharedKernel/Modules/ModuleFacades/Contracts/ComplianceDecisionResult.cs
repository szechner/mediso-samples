namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record ComplianceDecisionResult(
    Guid PaymentId,
    bool Approved,
    string ReviewedBy,
    DateTimeOffset ReviewedAt,
    string? Reason = null
);