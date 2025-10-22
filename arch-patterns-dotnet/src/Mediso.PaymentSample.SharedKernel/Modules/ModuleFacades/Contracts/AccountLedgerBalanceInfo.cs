namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record AccountLedgerBalanceInfo(
    Guid AccountId,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal NetBalance,
    DateTimeOffset CalculatedAt
);