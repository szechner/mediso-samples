namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

public abstract record PaymentStatusResponse
{
    /// <summary>Correlation ID used for tracking.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Current processing status.</summary>
    public string Status { get; init; } = string.Empty;
}