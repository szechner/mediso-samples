using System.Diagnostics;
using Marten;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.EventStore;

public class MartenEventStore : IEventStore
{
    private readonly IDocumentSession _session;
    private readonly ILogger<MartenEventStore> _logger;
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);

    public MartenEventStore(IDocumentSession session, ILogger<MartenEventStore> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AppendEventsAsync(Guid streamId, long expectedVersion, IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("EventStore.AppendEvents");
        activity?.SetTag(TracingConstants.Tags.StreamId, streamId);
        activity?.SetTag(TracingConstants.Tags.ExpectedVersion, expectedVersion);
        activity?.SetTag(TracingConstants.Tags.EventCount, events.Count());

        try
        {
            _logger.LogInformation(
                "Appending {EventCount} events to stream {StreamId} at expected version {ExpectedVersion}",
                events.Count(), streamId, expectedVersion);

            var eventArray = events.ToArray();
            foreach (var (evt, index) in eventArray.Select((e, i) => (e, i)))
            {
                activity?.SetTag($"{TracingConstants.Tags.Event}.{index}.Type", evt.GetType().Name);
            }

            // For new streams (expectedVersion -1), use Append without version check
            // For existing streams, use Append with expected version for optimistic concurrency
            if (expectedVersion == -1)
            {
                _session.Events.Append(streamId, eventArray);
            }
            else
            {
                _session.Events.Append(streamId, expectedVersion, eventArray);
            }
            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully appended {EventCount} events to stream {StreamId}",
                events.Count(), streamId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex,
                "Failed to append events to stream {StreamId} at expected version {ExpectedVersion}",
                streamId, expectedVersion);
            throw;
        }
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid streamId, long fromVersion = 0, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("EventStore.GetEvents");
        activity?.SetTag(TracingConstants.Tags.StreamId, streamId);
        activity?.SetTag(TracingConstants.Tags.FromVersion, fromVersion);

        try
        {
            _logger.LogInformation(
                "Retrieving events from stream {StreamId} starting from version {FromVersion}",
                streamId, fromVersion);

            var events = await _session.Events.FetchStreamAsync(streamId, fromVersion);
            var domainEvents = events.Select(e => e.Data).Cast<IDomainEvent>().ToArray();

            activity?.SetTag(TracingConstants.Tags.EventCount, domainEvents.Length);

            _logger.LogInformation(
                "Retrieved {EventCount} events from stream {StreamId}",
                domainEvents.Length, streamId);

            return domainEvents;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex,
                "Failed to retrieve events from stream {StreamId} starting from version {FromVersion}",
                streamId, fromVersion);
            throw;
        }
    }

    public async Task<T?> LoadAggregateAsync<T>(Guid aggregateId, CancellationToken cancellationToken = default) 
        where T : class, IAggregateRoot
    {
        using var activity = ActivitySource.StartActivity("EventStore.LoadAggregate");
        activity?.SetTag(TracingConstants.Tags.AggregateId, aggregateId);
        activity?.SetTag(TracingConstants.Tags.AggregateType, typeof(T).Name);

        try
        {
            _logger.LogInformation(
                "Loading aggregate {AggregateType} with ID {AggregateId}",
                typeof(T).Name, aggregateId);

            var aggregate = await _session.Events.AggregateStreamAsync<T>(aggregateId);

            if (aggregate != null)
            {
                activity?.SetTag(TracingConstants.Tags.AggregateVersion, aggregate.Version);
                _logger.LogInformation(
                    "Successfully loaded aggregate {AggregateType} with ID {AggregateId} at version {Version}",
                    typeof(T).Name, aggregateId, aggregate.Version);
            }
            else
            {
                _logger.LogWarning(
                    "Aggregate {AggregateType} with ID {AggregateId} not found",
                    typeof(T).Name, aggregateId);
            }

            return aggregate;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex,
                "Failed to load aggregate {AggregateType} with ID {AggregateId}",
                typeof(T).Name, aggregateId);
            throw;
        }
    }

    public async Task SaveAggregateAsync<T>(T aggregate, CancellationToken cancellationToken = default) 
        where T : class, IAggregateRoot
    {
        using var activity = ActivitySource.StartActivity("EventStore.SaveAggregate");
        activity?.SetTag(TracingConstants.Tags.AggregateId, aggregate.Id);
        activity?.SetTag(TracingConstants.Tags.AggregateType, typeof(T).Name);
        activity?.SetTag(TracingConstants.Tags.AggregateVersion, aggregate.Version);

        var uncommittedEvents = aggregate.GetUncommittedEvents().ToArray();
        activity?.SetTag(TracingConstants.Tags.EventCount, uncommittedEvents.Length);

        try
        {
            if (uncommittedEvents.Length == 0)
            {
                _logger.LogInformation(
                    "No uncommitted events to save for aggregate {AggregateType} with ID {AggregateId}",
                    typeof(T).Name, aggregate.Id);
                return;
            }

            _logger.LogInformation(
                "Saving aggregate {AggregateType} with ID {AggregateId} - {EventCount} uncommitted events",
                typeof(T).Name, aggregate.Id, uncommittedEvents.Length);

            foreach (var (evt, index) in uncommittedEvents.Select((e, i) => (e, i)))
            {
                activity?.SetTag($"{TracingConstants.Tags.Event}.{index}.Type", evt.GetType().Name);
            }

            // If this is a brand new stream (no persisted events yet), start the stream
            var persistedVersion = aggregate.Version - uncommittedEvents.Length;
            if (persistedVersion <= 0)
            {
                _session.Events.StartStream(aggregate.Id, uncommittedEvents);
            }
            else
            {
                // For existing streams, append with optimistic concurrency
                _session.Events.Append(aggregate.Id, persistedVersion, uncommittedEvents);
            }

            await _session.SaveChangesAsync(cancellationToken);

            aggregate.MarkEventsAsCommitted();

            _logger.LogInformation(
                "Successfully saved aggregate {AggregateType} with ID {AggregateId}",
                typeof(T).Name, aggregate.Id);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex,
                "Failed to save aggregate {AggregateType} with ID {AggregateId}",
                typeof(T).Name, aggregate.Id);
            throw;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}