namespace Mediso.PaymentSample.SharedKernel.Domain;

/// <summary>
/// Base interface for all domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the domain event was created
    /// </summary>
    DateTimeOffset CreatedAt { get; }
}
