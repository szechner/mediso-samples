namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record AccountBalanceInfo(
    Guid AccountId,
    string Currency,
    decimal Available,
    decimal Reserved,
    decimal Total
);