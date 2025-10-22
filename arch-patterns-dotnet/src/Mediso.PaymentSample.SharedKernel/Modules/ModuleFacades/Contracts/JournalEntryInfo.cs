namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record JournalEntryInfo(
    Guid EntryId,
    Guid PaymentId,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Currency,
    string Reference,
    DateTimeOffset CreatedAt
);