using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Payments.Module;

/// <summary>
/// Public interface for the Payments module - defines the contract for external modules
/// </summary>
public interface IPaymentsModule
{
    Task<PaymentId> CreatePaymentAsync(CreatePaymentCommand command, CancellationToken cancellationToken = default);
    Task<PaymentResult> GetPaymentAsync(PaymentId paymentId, CancellationToken cancellationToken = default);
    Task ProcessAMLResultAsync(PaymentId paymentId, AMLResult result, CancellationToken cancellationToken = default);
    Task ProcessFundsReservationAsync(PaymentId paymentId, ReservationResult result, CancellationToken cancellationToken = default);
    Task ProcessSettlementAsync(PaymentId paymentId, SettlementResult result, CancellationToken cancellationToken = default);
    Task CancelPaymentAsync(PaymentId paymentId, string cancelledBy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command for creating a payment - module boundary contract
/// </summary>
public sealed record CreatePaymentCommand(
    Money Amount,
    AccountId PayerAccountId,
    AccountId PayeeAccountId,
    string Reference
);

/// <summary>
/// Result of payment query - module boundary contract
/// </summary>
public sealed record PaymentResult(
    PaymentId PaymentId,
    Money Amount,
    AccountId PayerAccountId,
    AccountId PayeeAccountId,
    string Reference,
    PaymentState State,
    ReservationId? ReservationId = null,
    IReadOnlyList<PaymentEvent>? Events = null
);

/// <summary>
/// Payment event for external consumption
/// </summary>
public sealed record PaymentEvent(
    string EventType,
    DateTimeOffset OccurredAt,
    object Data
);

/// <summary>
/// AML screening result from Compliance module
/// </summary>
public sealed record AMLResult(
    bool Passed,
    string? Reason = null,
    string? Severity = null,
    string RuleSetVersion = "1.0"
);

/// <summary>
/// Funds reservation result from Accounts module
/// </summary>
public sealed record ReservationResult(
    bool Success,
    ReservationId? ReservationId = null,
    string? FailureReason = null
);

/// <summary>
/// Settlement result from external gateway
/// </summary>
public sealed record SettlementResult(
    bool Success,
    string Channel,
    string? ExternalReference = null,
    string? FailureReason = null
);