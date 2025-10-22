namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public sealed record ScreenPaymentRequest(
    Guid PaymentId,
    Guid PayerAccountId,
    Guid PayeeAccountId,
    decimal Amount,
    string Currency,
    string Reference
);