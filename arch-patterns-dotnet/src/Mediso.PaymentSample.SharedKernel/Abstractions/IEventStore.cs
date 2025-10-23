using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.SharedKernel.Abstractions;

/// <summary>
/// Interface for event store operations
/// </summary>
public interface IEventStore : IDisposable
{
    /// <summary>
    /// Appends events to a stream with optimistic concurrency control
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream</param>
    /// <param name="expectedVersion">The expected version for optimistic concurrency control</param>
    /// <param name="events">The domain events to append</param>
    /// <param name="correlationId">CorrelationId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AppendEventsAsync(Guid streamId, long expectedVersion, IEnumerable<IDomainEvent> events, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events from a stream starting from a specific version
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream</param>
    /// <param name="fromVersion">The version to start reading from (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of domain events</returns>
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid streamId, long fromVersion = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an aggregate from its event stream
    /// </summary>
    /// <typeparam name="T">The aggregate root type</typeparam>
    /// <param name="aggregateId">The unique identifier for the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The reconstructed aggregate or null if not found</returns>
    Task<T?> LoadAggregateAsync<T>(Guid aggregateId, CancellationToken cancellationToken = default) where T : class, IAggregateRoot;

    /// <summary>
    /// Saves an aggregate by appending its uncommitted events to the event stream
    /// </summary>
    /// <typeparam name="T">The aggregate root type</typeparam>
    /// <param name="aggregate">The aggregate to save</param>
    /// <param name="correlationId">CorrelationId</param>
    /// <param name="header">Metadata header</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAggregateAsync<T>(T aggregate, string correlationId, (string Key, object? Value)? header = null , CancellationToken cancellationToken = default) where T : class, IAggregateRoot;
}