namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record CreateJournalRequest(
    Guid PaymentId,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Currency,
    string Reference
);