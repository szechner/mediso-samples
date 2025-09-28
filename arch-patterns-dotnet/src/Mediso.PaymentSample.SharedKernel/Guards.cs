using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.SharedKernel;

public static class Guards
{
    public static Guid NotEmpty(Guid value, string? paramName = null)
        => value != Guid.Empty ? value : throw new DomainException($"{paramName ?? nameof(value)} must not be empty");


    public static string NotNullOrWhiteSpace(string? value, string? paramName = null)
        => !string.IsNullOrWhiteSpace(value) ? value! : throw new DomainException($"{paramName ?? nameof(value)} must not be empty");
}
