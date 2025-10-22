namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record ReservationResult(
    bool Success,
    Guid? ReservationId = null,
    string? FailureReason = null
);