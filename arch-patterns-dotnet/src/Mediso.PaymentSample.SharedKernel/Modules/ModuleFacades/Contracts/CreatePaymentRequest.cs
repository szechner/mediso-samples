using Mediso.PaymentSample.SharedKernel.Attributes;

namespace Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;

// ============ FACADE DTOs ============

/// <summary>
/// Request to create a new payment using saga orchestration.
/// Enhanced to support fraud detection and comprehensive payment processing.
/// </summary>
[RequireModuleAccess("Payments", "CreatePayment")]
public record CreatePaymentRequest
{
    /// <summary>Payment amount in the smallest currency unit.</summary>
    public decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code (e.g., USD, EUR, CZK).</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Payer account identifier (customer ID).</summary>
    public string PayerAccountId { get; init; } = string.Empty;

    /// <summary>Payee account identifier (merchant ID).</summary>
    public string PayeeAccountId { get; init; } = string.Empty;

    /// <summary>Payment reference or description.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Payment method (credit-card, bank-transfer, etc.).</summary>
    public string? PaymentMethod { get; init; }

    /// <summary>Idempotency key for duplicate request handling.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Additional metadata for processing context.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}