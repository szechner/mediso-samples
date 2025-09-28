using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Payments;

/// <summary>
/// Represents a payment aggregate root in the payment domain
/// </summary>
public sealed class Payment : Aggregate<PaymentId>
{
    /// <summary>
    /// The payment amount and currency
    /// </summary>
    public Money Amount { get; private set; }
    
    /// <summary>
    /// The account ID of the party making the payment
    /// </summary>
    public AccountId PayerAccountId { get; private set; } = default!;
    
    /// <summary>
    /// The account ID of the party receiving the payment
    /// </summary>
    public AccountId PayeeAccountId { get; private set; } = default!;
    
    /// <summary>
    /// Reference or description for the payment
    /// </summary>
    public string Reference { get; private set; } = string.Empty;
    
    /// <summary>
    /// Current state of the payment in its lifecycle
    /// </summary>
    public PaymentState State { get; private set; } = PaymentState.Requested;
    
    /// <summary>
    /// ID of the funds reservation associated with this payment, if any
    /// </summary>
    public ReservationId? ReservationId { get; private set; }

    /// <summary>
    /// Private constructor for ORM and event sourcing reconstruction
    /// </summary>
    private Payment()
    {
    }


    /// <summary>
    /// Creates a new payment with the specified details
    /// </summary>
    /// <param name="id">Unique payment identifier</param>
    /// <param name="amount">Payment amount and currency</param>
    /// <param name="payer">Account ID of the payer</param>
    /// <param name="payee">Account ID of the payee</param>
    /// <param name="reference">Payment reference or description</param>
    /// <returns>A new payment instance in Requested state</returns>
    /// <exception cref="DomainException">Thrown when payer and payee are the same account</exception>
    public static Payment Create(PaymentId id, Money amount, AccountId payer, AccountId payee, string reference)
    {
        if (payer.Value == payee.Value)
            throw new DomainException("Payer and Payee must differ");


        var payment = new Payment();
        payment.Raise(new PaymentRequested(id, amount.EnsurePositive(), payer, payee, reference, DateTimeOffset.UtcNow));
        return payment;
    }


    /// <summary>
    /// Marks the payment as having passed anti-money laundering checks
    /// </summary>
    /// <param name="ruleSetVersion">Version of the AML rule set used for validation</param>
    /// <exception cref="DomainException">Thrown when the payment is not in an allowed state</exception>
    public void MarkAMLPassed(string ruleSetVersion)
    {
        EnsureState(PaymentState.Requested, PaymentState.Flagged, PaymentState.Released);
        Raise(new AMLPassed(Id, ruleSetVersion, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Flags the payment for compliance review
    /// </summary>
    /// <param name="reason">Reason for flagging the payment</param>
    /// <param name="severity">Severity level of the flag</param>
    /// <exception cref="DomainException">Thrown when the payment is not in Requested state</exception>
    public void Flag(string reason, string severity)
    {
        EnsureState(PaymentState.Requested);
        Raise(new PaymentFlagged(Id, reason, severity, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Manually releases a payment that was previously flagged
    /// </summary>
    /// <exception cref="DomainException">Thrown when the payment is not in Flagged state</exception>
    public void ReleaseAfterFlag()
    {
        EnsureState(PaymentState.Flagged);
        Raise(new AMLPassed(Id, RuleSetVersion: "manual-release", DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Reserves funds for the payment
    /// </summary>
    /// <param name="reservationId">Unique identifier for the funds reservation</param>
    /// <exception cref="DomainException">Thrown when the payment is not in Requested or Released state</exception>
    public void ReserveFunds(ReservationId reservationId)
    {
        EnsureState(PaymentState.Requested, PaymentState.Released);
        Raise(new FundsReserved(Id, reservationId, Amount, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Records that the funds reservation has failed
    /// </summary>
    /// <param name="reason">Reason for the reservation failure</param>
    /// <exception cref="DomainException">Thrown when the payment is not in Requested or Released state</exception>
    public void FailReservation(string reason)
    {
        EnsureState(PaymentState.Requested, PaymentState.Released);
        Raise(new FundsReservationFailed(Id, reason, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Journals the payment with the specified ledger entries
    /// </summary>
    /// <param name="entries">List of ledger entries for the payment</param>
    /// <exception cref="DomainException">Thrown when the payment is not in Reserved state or no entries provided</exception>
    public void Journal(IReadOnlyList<LedgerEntry> entries)
    {
        EnsureState(PaymentState.Reserved);
        if (entries.Count == 0) throw new DomainException("Journal requires entries");
        Raise(new PaymentJournaled(Id, entries, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Settles the payment through the specified channel
    /// </summary>
    /// <param name="channel">Settlement channel used for the payment</param>
    /// <param name="externalRef">Optional external reference from the settlement system</param>
    /// <exception cref="DomainException">Thrown when the payment is not in Journaled state</exception>
    public void Settle(string channel, string? externalRef)
    {
        EnsureState(PaymentState.Journaled);
        Raise(new PaymentSettled(Id, channel, externalRef, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Cancels the payment
    /// </summary>
    /// <param name="by">Identifier of who cancelled the payment</param>
    /// <exception cref="DomainException">Thrown when the payment is not in an allowed state for cancellation</exception>
    public void Cancel(string by)
    {
        EnsureState(PaymentState.Requested, PaymentState.Flagged, PaymentState.Released);
        Raise(new PaymentCancelled(Id, by, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Declines the payment with the specified reason
    /// </summary>
    /// <param name="reason">Reason for declining the payment</param>
    /// <exception cref="DomainException">Thrown when the payment is not in an allowed state for decline</exception>
    public void Decline(string reason)
    {
        EnsureState(PaymentState.Requested, PaymentState.Flagged, PaymentState.Released, PaymentState.Reserved);
        Raise(new PaymentDeclined(Id, reason, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Marks the payment as failed with the specified reason
    /// </summary>
    /// <param name="reason">Reason for the payment failure</param>
    public void Fail(string reason)
    {
        // Can fail from multiple states (e.g., settlement failure)
        Raise(new PaymentFailed(Id, reason, DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Handles domain events to update payment state
    /// </summary>
    /// <param name="event">Domain event to process</param>
    protected override void When(IDomainEvent @event)
    {
        switch (@event)
        {
            case PaymentRequested e:
                Id = e.PaymentId;
                Amount = e.Amount;
                PayerAccountId = e.PayerAccountId;
                PayeeAccountId = e.PayeeAccountId;
                Reference = e.Reference;
                State = PaymentState.Requested;
                break;


            case PaymentFlagged:
                State = PaymentState.Flagged;
                break;


            case AMLPassed:
                State = State == PaymentState.Flagged ? PaymentState.Released : State; // If previously flagged, transition to Released; else stay/request → Reserved later
                break;

            case FundsReserved e:
                ReservationId = e.ReservationId;
                State = PaymentState.Reserved;
                break;

            case FundsReservationFailed:
                break; // stay in Requested/Released, decision left to Application (Decline or retry)

            case PaymentJournaled:
                State = PaymentState.Journaled;
                break;


            case PaymentSettled:
                State = PaymentState.Settled;
                break;

            case PaymentCancelled:
                State = PaymentState.Declined;
                break;

            case PaymentDeclined:
                State = PaymentState.Declined;
                break;

            case PaymentFailed:
                State = PaymentState.Failed;
                break;
        }
    }


    private void EnsureState(params PaymentState[] allowed)
    {
        if (!allowed.Contains(State))
            throw new DomainException($"Operation not allowed in state {State}");
    }
}