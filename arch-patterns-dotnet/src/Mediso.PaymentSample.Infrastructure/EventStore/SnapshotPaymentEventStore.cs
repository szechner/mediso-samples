using System.Diagnostics;
using Marten;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.Infrastructure.Monitoring;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Mediso.PaymentSample.Infrastructure.EventStore;

/// <summary>
/// Event store decorator that implements custom snapshotting every 5 events for Payment aggregates
/// </summary>
public class SnapshotPaymentEventStore : IEventStore
{
    private readonly MartenEventStore _innerEventStore;
    private readonly IDocumentSession _session;
    private readonly ILogger<SnapshotPaymentEventStore> _logger;
    private readonly PaymentMetrics _metrics;
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);
    
    private const int SnapshotFrequency = 5;

    public SnapshotPaymentEventStore(
        MartenEventStore innerEventStore, 
        IDocumentSession session,
        ILogger<SnapshotPaymentEventStore> logger,
        PaymentMetrics metrics)
    {
        _innerEventStore = innerEventStore;
        _session = session;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task AppendEventsAsync(Guid streamId, long expectedVersion, IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        
        try
        {
            // Delegate to the inner event store
            await _innerEventStore.AppendEventsAsync(streamId, expectedVersion, events, cancellationToken);
            success = true;
            
            // Record metrics for event appending
            var eventCount = events.Count();
            _metrics.RecordEventsAppended(eventCount, "Payment");
            
            // Check if we need to create a snapshot
            var newVersion = expectedVersion == -1 ? eventCount : expectedVersion + eventCount;
            
            if (ShouldCreateSnapshot(newVersion))
            {
                await CreateSnapshotIfPaymentAggregate(streamId, cancellationToken);
            }
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordEventStoreOperation("append", stopwatch.ElapsedMilliseconds, success);
        }
    }

    public Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid streamId, long fromVersion = 0, CancellationToken cancellationToken = default)
    {
        return _innerEventStore.GetEventsAsync(streamId, fromVersion, cancellationToken);
    }

    public async Task<T?> LoadAggregateAsync<T>(Guid aggregateId, CancellationToken cancellationToken = default) where T : class, IAggregateRoot
    {
        // First try to load from snapshot if available
        if (typeof(T) == typeof(Payment))
        {
            var snapshot = await TryLoadFromSnapshot<T>(aggregateId, cancellationToken);
            if (snapshot != null)
            {
                _logger.LogInformation("Loaded aggregate {AggregateType} {AggregateId} from snapshot", typeof(T).Name, aggregateId);
                return snapshot;
            }
        }
        
        // Fall back to loading from events
        return await _innerEventStore.LoadAggregateAsync<T>(aggregateId, cancellationToken);
    }

    public async Task SaveAggregateAsync<T>(T aggregate, CancellationToken cancellationToken = default) where T : class, IAggregateRoot
    {
        await _innerEventStore.SaveAggregateAsync(aggregate, cancellationToken);
        
        // Create snapshot if needed
        if (ShouldCreateSnapshot(aggregate.Version) && typeof(T) == typeof(Payment))
        {
            await CreateSnapshot(aggregate, cancellationToken);
        }
    }

    private bool ShouldCreateSnapshot(long version)
    {
        return version > 0 && version % SnapshotFrequency == 0;
    }

    private async Task CreateSnapshotIfPaymentAggregate(Guid streamId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("EventStore.CreateSnapshot");
        activity?.SetTag(TracingConstants.Tags.StreamId, streamId);
        
        try
        {
            // Try to load the aggregate and create a snapshot
            var payment = await _innerEventStore.LoadAggregateAsync<Payment>(streamId, cancellationToken);
            if (payment != null)
            {
                await CreateSnapshot(payment, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create snapshot for stream {StreamId}", streamId);
        }
    }

    private async Task CreateSnapshot<T>(T aggregate, CancellationToken cancellationToken) where T : class, IAggregateRoot
    {
        using var activity = ActivitySource.StartActivity("EventStore.SaveSnapshot");
        activity?.SetTag(TracingConstants.Tags.AggregateId, aggregate.Id);
        activity?.SetTag(TracingConstants.Tags.AggregateVersion, aggregate.Version);
        
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        
        try
        {
            // Store the aggregate snapshot as a separate document
            var snapshot = new PaymentSnapshot
            {
                Id = aggregate.Id,
                AggregateVersion = aggregate.Version,
                SnapshotData = JsonConvert.SerializeObject(aggregate),
                CreatedAt = DateTime.UtcNow
            };
            
            _session.Store(snapshot);
            await _session.SaveChangesAsync(cancellationToken);
            success = true;
            
            // Record metrics
            _metrics.RecordSnapshotCreated(typeof(T).Name, aggregate.Version);
            
            _logger.LogInformation("Created snapshot for {AggregateType} {AggregateId} at version {Version}", 
                typeof(T).Name, aggregate.Id, aggregate.Version);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to create snapshot for {AggregateType} {AggregateId}", 
                typeof(T).Name, aggregate.Id);
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordSnapshotOperation("create", stopwatch.ElapsedMilliseconds, success);
        }
    }

    private async Task<T?> TryLoadFromSnapshot<T>(Guid aggregateId, CancellationToken cancellationToken) where T : class, IAggregateRoot
    {
        using var activity = ActivitySource.StartActivity("EventStore.LoadSnapshot");
        activity?.SetTag(TracingConstants.Tags.AggregateId, aggregateId);
        
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        
        try
        {
            var snapshot = await _session.LoadAsync<PaymentSnapshot>(aggregateId, cancellationToken);
            if (snapshot?.SnapshotData != null)
            {
                var aggregate = JsonConvert.DeserializeObject<T>(snapshot.SnapshotData);
                if (aggregate != null)
                {
                    success = true;
                    _logger.LogInformation("Loaded snapshot for {AggregateType} {AggregateId} at version {Version}",
                        typeof(T).Name, aggregateId, snapshot.AggregateVersion);
                    return aggregate;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load snapshot for {AggregateType} {AggregateId}", typeof(T).Name, aggregateId);
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordSnapshotOperation("load", stopwatch.ElapsedMilliseconds, success);
        }
        
        return null;
    }

    public void Dispose()
    {
        _innerEventStore.Dispose();
    }
}