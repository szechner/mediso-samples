using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Domain.Payments;

public sealed class Payment : Aggregate<PaymentId>
{
    public Money Amount { get; private set; }
    public AccountId PayerAccountId { get; private set; }
    public AccountId PayeeAccountId { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public PaymentState State { get; private set; } = PaymentState.Requested;
    public ReservationId? ReservationId { get; private set; }


    private Payment()
    {
    }


    public static Payment Create(PaymentId id, Money amount, AccountId payer, AccountId payee, string reference)
    {
        if (payer.Value == payee.Value)
            throw new DomainException("Payer and Payee must differ");


        var payment = new Payment();
        payment.Raise(new PaymentRequested(id, amount.EnsurePositive(), payer, payee, reference, DateTimeOffset.UtcNow));
        return payment;
    }


    public void MarkAMLPassed(string ruleSetVersion)
    {
        EnsureState(PaymentState.Requested, PaymentState.Flagged, PaymentState.Released);
        Raise(new AMLPassed(Id, ruleSetVersion, DateTimeOffset.UtcNow));
    }


    public void Flag(string reason, string severity)
    {
        EnsureState(PaymentState.Requested);
        Raise(new PaymentFlagged(Id, reason, severity, DateTimeOffset.UtcNow));
    }


    public void ReleaseAfterFlag()
    {
        EnsureState(PaymentState.Flagged);
        Raise(new AMLPassed(Id, RuleSetVersion: "manual-release", DateTimeOffset.UtcNow));
    }


    public void ReserveFunds(ReservationId reservationId)
    {
        EnsureState(PaymentState.Requested, PaymentState.Released);
        Raise(new FundsReserved(Id, reservationId, Amount, DateTimeOffset.UtcNow));
    }


    public void FailReservation(string reason)
    {
        EnsureState(PaymentState.Requested, PaymentState.Released);
        Raise(new FundsReservationFailed(Id, reason, DateTimeOffset.UtcNow));
    }


    public void Journal(IReadOnlyList<LedgerEntry> entries)
    {
        EnsureState(PaymentState.Reserved);
        if (entries.Count == 0) throw new DomainException("Journal requires entries");
        Raise(new PaymentJournaled(Id, entries, DateTimeOffset.UtcNow));
    }


    public void Settle(string channel, string? externalRef)
    {
        EnsureState(PaymentState.Journaled);
        Raise(new PaymentSettled(Id, channel, externalRef, DateTimeOffset.UtcNow));
    }


    public void Cancel(string by)
    {
        EnsureState(PaymentState.Requested, PaymentState.Flagged, PaymentState.Released);
        Raise(new PaymentCancelled(Id, by, DateTimeOffset.UtcNow));
    }


    public void Decline(string reason)
    {
        EnsureState(PaymentState.Requested, PaymentState.Flagged, PaymentState.Released, PaymentState.Reserved);
        Raise(new PaymentDeclined(Id, reason, DateTimeOffset.UtcNow));
    }

    public void Fail(string reason)
    {
        // Can fail from multiple states (e.g., settlement failure)
        Raise(new PaymentFailed(Id, reason, DateTimeOffset.UtcNow));
    }


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