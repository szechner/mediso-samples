namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record JournalResult(
    bool Success,
    Guid? JournalId = null,
    string? FailureReason = null
);