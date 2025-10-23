using Mediso.PaymentSample.Application.Modules.Payments.Contracts;

namespace Mediso.PaymentSample.Application.Modules.Payments.Ports;

/// <summary>
/// Primary port for payment command handling (driving adapter interface).
/// Defines the contract for incoming commands from external systems.
/// Following hexagonal architecture, this represents the "use case" boundary.
/// </summary>
public interface IInitiatePaymentHandler
{
    /// <summary>
    /// Handles payment initiation with comprehensive validation and idempotency.
    /// </summary>
    /// <param name="command">Payment initiation command</param>
    /// <param name="cancellationToken">Cancellation token for operation timeout</param>
    /// <returns>Payment initiation result with created payment details</returns>
    Task<InitiatePaymentResponse> HandleAsync(
        InitiatePaymentCommand command, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Primary port for payment reservation handling.
/// Responsible for fund reservation with fraud detection integration.
/// </summary>
public interface IReservePaymentHandler
{
    /// <summary>
    /// Handles payment reservation with risk assessment and authorization.
    /// </summary>
    /// <param name="command">Payment reservation command</param>
    /// <param name="cancellationToken">Cancellation token for operation timeout</param>
    /// <returns>Payment reservation result with reservation status</returns>
    Task<ReservePaymentResponse> HandleAsync(
        ReservePaymentCommand command, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Primary port for payment settlement (capture) handling.
/// Manages the final step of payment processing.
/// </summary>
public interface ISettlePaymentHandler
{
    /// <summary>
    /// Handles payment settlement with reconciliation and fee calculation.
    /// </summary>
    /// <param name="command">Payment settlement command</param>
    /// <param name="cancellationToken">Cancellation token for operation timeout</param>
    /// <returns>Payment settlement result with final status</returns>
    Task<SettlePaymentResponse> HandleAsync(
        SettlePaymentCommand command, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Primary port for payment cancellation handling.
/// Handles cancellations with refund processing coordination.
/// </summary>
public interface ICancelPaymentHandler
{
    /// <summary>
    /// Handles payment cancellation with refund processing if required.
    /// </summary>
    /// <param name="command">Payment cancellation command</param>
    /// <param name="cancellationToken">Cancellation token for operation timeout</param>
    /// <returns>Payment cancellation result with refund information</returns>
    Task<CancelPaymentResponse> HandleAsync(
        CancelPaymentCommand command, 
        CancellationToken cancellationToken = default);
}