using System;

namespace Mediso.PaymentSample.SharedKernel.Domain;

/// <summary>
/// Marker interface for integration events that cross module boundaries
/// These events are published across modules for coordination
/// </summary>
public interface IIntegrationEvent
{
    DateTimeOffset OccurredAt { get; }
    Guid EventId { get; }
    string EventType { get; }
}

/// <summary>
/// Base record for integration events with common properties
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public virtual string EventType => GetType().Name;
}