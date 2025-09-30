using System;

namespace Mediso.PaymentSample.SharedKernel.Domain;

/// <summary>
/// Marker interface for integration events that cross module boundaries
/// These events are published across modules for coordination
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Gets the timestamp when the integration event was created
    /// </summary>
    DateTimeOffset CreatedAt { get; }
    
    /// <summary>
    /// Gets the unique identifier for this event instance
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// Gets the type name of the event
    /// </summary>
    string EventType { get; }
    
    /// <summary>
    /// Gets the version of the event schema for backward compatibility
    /// </summary>
    int Version { get; }
    
    /// <summary>
    /// Gets the correlation identifier to track related events and operations
    /// </summary>
    string CorrelationId { get; }
}

/// <summary>
/// Base record for integration events with common properties
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event instance
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Timestamp when the event was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Type name of the event (defaults to class name)
    /// </summary>
    public virtual string EventType => GetType().Name;
    
    /// <summary>
    /// Version of the event schema (defaults to 1)
    /// </summary>
    public virtual int Version => 1;
    
    /// <summary>
    /// Correlation identifier for tracking related events (auto-generated if not provided)
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
}
