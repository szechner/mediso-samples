using System.Diagnostics;
using Marten;
using Microsoft.Extensions.Logging;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;

namespace Mediso.PaymentSample.Infrastructure.EventStore;

public class MartenEventStore : IEventStore, IDisposable
{
    private readonly IDocumentSession _session;
    private readonly ILogger<MartenEventStore> _logger;
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);

    public MartenEventStore(IDocumentSession session, ILogger<MartenEventStore> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Přidá eventy do streamu s ochranou proti kolizi verzí.
    /// Pokud expectedVersion == -1 nebo není známá, zjistí se verze streamu z DB.
    /// Pro nový stream použije StartStream(...).
    /// </summary>
    public async Task AppendEventsAsync(
        Guid streamId,
        long expectedVersion,
        IEnumerable<IDomainEvent> events,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (events is null) throw new ArgumentNullException(nameof(events));
        var eventArray = events as IDomainEvent[] ?? events.ToArray();
        using var activity = ActivitySource.StartActivity("EventStore.AppendEvents");
        activity?.SetTag(TracingConstants.Tags.StreamId, streamId);
        activity?.SetTag(TracingConstants.Tags.ExpectedVersion, expectedVersion);
        foreach (var (evt, index) in eventArray.Select((e, i) => (e, i)))
        {
            activity?.SetTag($"{TracingConstants.Tags.Event}.{index}.Type", evt.GetType().Name);
        }

        _session.CorrelationId = correlationId;

        try
        {
            _logger.LogInformation("Appending {Count} event(s) to stream {StreamId}. ExpectedVersion={ExpectedVersion}",
                eventArray.Length, streamId, expectedVersion);

            for (var i = 0; i < eventArray.Length; i++)
                activity?.SetTag($"{TracingConstants.Tags.Event}.{i}.Type", eventArray[i].GetType().Name);

            var state = await _session.Events.FetchStreamStateAsync(streamId, cancellationToken);

            if (expectedVersion >= 0 && (state?.Version ?? 0) != expectedVersion)
            {
                throw new InvalidOperationException($"Concurrency conflict: expected {expectedVersion}, actual {(state?.Version ?? 0)} for stream {streamId}");
            }


            if ((expectedVersion < 0 && (state == null || state.Version == 0)) ||
                (expectedVersion == 0 && (await _session.Events.FetchStreamStateAsync(streamId, cancellationToken))?.Version == 0))
            {
                // nový stream
                _session.Events.StartStream(streamId, eventArray);
            }
            else
            {
                var versionToUse = expectedVersion >= 0
                    ? expectedVersion
                    : (state!.Version); // state.Version = aktuální počet persistovaných eventů

                _session.Events.Append(streamId, versionToUse, eventArray);
            }

            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Appended {Cnt} event(s) to stream {StreamId}", eventArray.Length, streamId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to append events to stream {StreamId}", streamId);
            throw;
        }
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(
        Guid streamId,
        long fromVersion = 0,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("EventStore.GetEvents");
        activity?.SetTag(TracingConstants.Tags.StreamId, streamId);
        activity?.SetTag(TracingConstants.Tags.FromVersion, fromVersion);

        try
        {
            _logger.LogInformation("Retrieving events from stream {StreamId} from version {FromVersion}", streamId, fromVersion);

            var events = await _session.Events.FetchStreamAsync(streamId, fromVersion, token: cancellationToken);
            var domainEvents = events.Select(e => e.Data).OfType<IDomainEvent>().ToArray();

            activity?.SetTag(TracingConstants.Tags.EventCount, domainEvents.Length);
            _logger.LogInformation("Retrieved {Cnt} event(s) from stream {StreamId}", domainEvents.Length, streamId);

            return domainEvents;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to retrieve events from stream {StreamId}", streamId);
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
            _logger.LogInformation("Loading aggregate {Agg} with ID {Id}", typeof(T).Name, aggregateId);

            var aggregate = await _session.Events.AggregateStreamAsync<T>(aggregateId, token: cancellationToken);

            if (aggregate != null)
            {
                activity?.SetTag(TracingConstants.Tags.AggregateVersion, aggregate.Version);
                _logger.LogInformation("Loaded {Agg} {Id} at version {Ver}. Uncommitted={Unc}",
                    typeof(T).Name, aggregateId, aggregate.Version, aggregate.GetUncommittedEvents().Count());
            }
            else
            {
                _logger.LogWarning("{Agg} {Id} not found", typeof(T).Name, aggregateId);
            }

            return aggregate;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to load aggregate {Agg} {Id}", typeof(T).Name, aggregateId);
            throw;
        }
    }

    /// <summary>
    /// Uloží uncommitted eventy agregátu do jeho streamu s ochranou proti kolizi verzí.
    /// </summary>
    public async Task SaveAggregateAsync<T>(
        T aggregate,
        string correlationId,
        (string Key, object? Value)? header = null,
        CancellationToken cancellationToken = default)
        where T : class, IAggregateRoot
    {
        if (aggregate is null) throw new ArgumentNullException(nameof(aggregate));

        using var activity = ActivitySource.StartActivity("EventStore.SaveAggregate");
        activity?.SetTag(TracingConstants.Tags.AggregateId, aggregate.Id);
        activity?.SetTag(TracingConstants.Tags.AggregateType, typeof(T).Name);
        activity?.SetTag(TracingConstants.Tags.AggregateVersion, aggregate.Version);

        var uncommitted = aggregate.GetUncommittedEvents().ToArray();
        activity?.SetTag(TracingConstants.Tags.EventCount, uncommitted.Length);

        _session.CorrelationId = correlationId;
        
        if (header is { Value: { } hv })
        {
            _session.SetHeader(header.Value.Key, hv); // ✔ předáváme skutečnou hodnotu
        }

        try
        {
            if (uncommitted.Length == 0)
            {
                _logger.LogInformation("No uncommitted events for {Agg} {Id}", typeof(T).Name, aggregate.Id);
                return;
            }

            for (var i = 0; i < uncommitted.Length; i++)
            {
                activity?.SetTag($"{TracingConstants.Tags.Event}.{i}.Type", uncommitted[i].GetType().Name);
            }

            var state = await _session.Events.FetchStreamStateAsync(aggregate.Id, cancellationToken);

                var lastPersistedVersion = aggregate.Version - uncommitted.Length;
            
            
            if (state == null)
            {
                _logger.LogInformation("Starting new stream for {Agg} {Id}", typeof(T).Name, aggregate.Id);
                _session.Events.StartStream(aggregate.Id, uncommitted);
            }
            else
            {
                // Append s očekávanou verzí = aktuální verze v DB
                _logger.LogInformation("Appending {Count} event(s) to {Aggregate} {Id} at expected version {Ver}",
                    uncommitted.Length, typeof(T).Name, aggregate.Id, state.Version);

                var stream = await _session.Events.FetchForWriting<T>(aggregate.Id, cancellationToken);
                
                stream.AppendMany(uncommitted);
            }

            await _session.SaveChangesAsync(cancellationToken);

            // úspěch → označ eventy jako commitnuté v agregátu
            aggregate.MarkEventsAsCommitted();

            _logger.LogInformation("Saved {Agg} {Id}", typeof(T).Name, aggregate.Id);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to save aggregate {Agg} {Id}", typeof(T).Name, aggregate.Id);
            throw;
        }
    }

    public void Dispose() => _session?.Dispose();
}
