using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain;

// Integration events for cross-module communication
// These events are published when modules need to coordinate with each other

/// <summary>
/// Published by Payments module when a payment is created and needs AML screening
/// </summary>
public sealed record PaymentCreatedIntegrationEvent(
    PaymentId PaymentId,
    AccountId PayerAccountId,
    AccountId PayeeAccountId,
    Money Amount,
    string Reference
) : IntegrationEvent
{
    /// <summary>
    /// Version 2 includes enhanced payment information
    /// </summary>
    public override int Version => 2;
};

/// <summary>
/// Published by Compliance module when AML screening is completed
/// </summary>
public sealed record AMLScreeningCompletedIntegrationEvent(
    PaymentId PaymentId,
    bool Passed,
    string RuleSetVersion,
    string? Reason = null,
    string? Severity = null
) : IntegrationEvent
{
    /// <summary>
    /// Version 1 - initial AML screening result format
    /// </summary>
    public override int Version => 1;
};

/// <summary>
/// Published by Payments module when funds need to be reserved
/// </summary>
public sealed record FundsReservationRequestedIntegrationEvent(
    PaymentId PaymentId,
    AccountId AccountId,
    Money Amount
) : IntegrationEvent;

/// <summary>
/// Published by Accounts module when funds reservation is completed
/// </summary>
public sealed record FundsReservationCompletedIntegrationEvent(
    PaymentId PaymentId,
    AccountId AccountId,
    bool Success,
    ReservationId? ReservationId = null,
    string? FailureReason = null
) : IntegrationEvent;

/// <summary>
/// Published by Payments module when journal entries need to be created
/// </summary>
public sealed record JournalEntriesRequestedIntegrationEvent(
    PaymentId PaymentId,
    AccountId DebitAccount,
    AccountId CreditAccount,
    Money Amount,
    string Reference
) : IntegrationEvent;

/// <summary>
/// Published by Ledger module when journal entries are created
/// </summary>
public sealed record JournalEntriesCreatedIntegrationEvent(
    PaymentId PaymentId,
    IReadOnlyList<LedgerEntryId> EntryIds,
    bool Success,
    string? FailureReason = null
) : IntegrationEvent;

/// <summary>
/// Published by Payments module when settlement is requested
/// </summary>
public sealed record SettlementRequestedIntegrationEvent(
    PaymentId PaymentId,
    Money Amount,
    string Channel
) : IntegrationEvent;

/// <summary>
/// Published by Settlement module when settlement is completed
/// </summary>
public sealed record SettlementCompletedIntegrationEvent(
    PaymentId PaymentId,
    bool Success,
    string Channel,
    string? ExternalReference = null,
    string? FailureReason = null
) : IntegrationEvent;

/// <summary>
/// Published by Payments module when payment status changes (for notifications)
/// </summary>
public sealed record PaymentStatusChangedIntegrationEvent(
    PaymentId PaymentId,
    PaymentState NewState,
    PaymentState? PreviousState = null,
    string? Reason = null
) : IntegrationEvent;

/// <summary>
/// Published by any module when account balance changes
/// </summary>
public sealed record AccountBalanceChangedIntegrationEvent(
    AccountId AccountId,
    decimal PreviousBalance,
    decimal NewBalance,
    string ChangeReason,
    string ModuleName
) : IntegrationEvent;

/// <summary>
/// Published by Payments module when a payment fails and needs compensation
/// </summary>
public sealed record PaymentCompensationRequestedIntegrationEvent(
    PaymentId PaymentId,
    string FailureReason,
    ReservationId? ReservationIdToRelease = null,
    IReadOnlyList<LedgerEntryId>? JournalEntriesToReverse = null
) : IntegrationEvent;

/// <summary>
/// Published by any module when high-risk activity is detected
/// </summary>
public sealed record HighRiskActivityDetectedIntegrationEvent(
    string ModuleName,
    AccountId AccountId,
    string ActivityType,
    string RiskReason,
    decimal RiskScore,
    PaymentId? RelatedPaymentId = null
) : IntegrationEvent;