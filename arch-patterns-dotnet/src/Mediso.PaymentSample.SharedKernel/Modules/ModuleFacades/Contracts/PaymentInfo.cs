namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record PaymentInfo(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    Guid PayerAccountId,
    Guid PayeeAccountId,
    string Reference,
    string Status,
    DateTimeOffset CreatedAt
);