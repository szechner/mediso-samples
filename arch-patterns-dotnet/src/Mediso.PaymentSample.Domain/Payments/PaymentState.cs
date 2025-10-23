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

/// <summary>
/// Alias for PaymentState for compatibility with application layer
/// </summary>
public enum PaymentStatus
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
