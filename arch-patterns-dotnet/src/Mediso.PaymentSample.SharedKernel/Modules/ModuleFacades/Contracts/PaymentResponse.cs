namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

/// <summary>
/// Response from payment creation with saga orchestration status.
/// Provides comprehensive information about the initiated payment process.
/// </summary>
public record PaymentResponse
{
    /// <summary>Generated payment identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Payment amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Payment currency.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Payer account identifier.</summary>
    public string PayerAccountId { get; init; } = string.Empty;

    /// <summary>Payee account identifier.</summary>
    public string PayeeAccountId { get; init; } = string.Empty;

    /// <summary>Payment reference.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Current payment state (domain aggregate state).</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>Payment reserved timestamp.</summary>
    public DateTimeOffset? RequestedAt { get; init; }
    
    /// <summary>Payment created timestamp.</summary>
    public DateTimeOffset? SettledAt { get; init; }
    
    /// <summary>Reason for decline if the payment was declined.</summary>
    public string? DeclinedReason { get; init; }
}