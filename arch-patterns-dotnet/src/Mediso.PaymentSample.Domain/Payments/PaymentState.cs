namespace Mediso.PaymentSample.Domain.Payments;

public enum PaymentState
{
    Requested = 0,
    Flagged = 1,
    Released = 2,
    Declined = 3,
    Reserved = 4,
    Journaled = 5,
    Settled = 6,
    Failed = 7
}