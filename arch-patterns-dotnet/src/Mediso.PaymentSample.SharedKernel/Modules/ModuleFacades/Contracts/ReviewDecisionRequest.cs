namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record ReviewDecisionRequest(
    bool Approved,
    string ReviewedBy,
    string? Reason = null
);