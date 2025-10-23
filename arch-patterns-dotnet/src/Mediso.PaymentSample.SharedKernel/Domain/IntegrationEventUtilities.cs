using System.Collections.Concurrent;

namespace Mediso.PaymentSample.SharedKernel.Domain;

/// <summary>
/// Utilities for working with integration events, correlation tracking, and event versioning
/// </summary>
public static class IntegrationEventUtilities
{
    /// <summary>
    /// Creates an integration event with a specific correlation ID to link related events
    /// </summary>
    /// <typeparam name="TEvent">Type of integration event to create</typeparam>
    /// <param name="eventFactory">Factory function to create the event</param>
    /// <param name="correlationId">Correlation ID to assign to the event</param>
    /// <returns>Integration event with the specified correlation ID</returns>
    public static TEvent WithCorrelationId<TEvent>(Func<TEvent> eventFactory, string correlationId) 
        where TEvent : IntegrationEvent
    {
        var integrationEvent = eventFactory();
        return integrationEvent with { CorrelationId = correlationId };
    }

    /// <summary>
    /// Creates an integration event that correlates with another event
    /// </summary>
    /// <typeparam name="TEvent">Type of integration event to create</typeparam>
    /// <param name="eventFactory">Factory function to create the event</param>
    /// <param name="correlatedEvent">Existing event to correlate with</param>
    /// <returns>Integration event with the same correlation ID as the correlated event</returns>
    public static TEvent CorrelateWith<TEvent>(Func<TEvent> eventFactory, IIntegrationEvent correlatedEvent) 
        where TEvent : IntegrationEvent
    {
        return WithCorrelationId(eventFactory, correlatedEvent.CorrelationId);
    }

    /// <summary>
    /// Checks if two events are correlated (have the same correlation ID)
    /// </summary>
    /// <param name="event1">First event to compare</param>
    /// <param name="event2">Second event to compare</param>
    /// <returns>True if events share the same correlation ID</returns>
    public static bool AreCorrelated(IIntegrationEvent event1, IIntegrationEvent event2)
    {
        return string.Equals(event1.CorrelationId, event2.CorrelationId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates a new correlation ID for starting a new event correlation chain
    /// </summary>
    /// <returns>New correlation ID as a string</returns>
    public static string NewCorrelationId() => Guid.NewGuid().ToString();

    /// <summary>
    /// Extracts correlation ID from an event or generates a new one if not present
    /// </summary>
    /// <param name="integrationEvent">Event to extract correlation ID from</param>
    /// <returns>Correlation ID or new GUID if not present</returns>
    public static string GetOrGenerateCorrelationId(IIntegrationEvent? integrationEvent)
    {
        return integrationEvent?.CorrelationId ?? NewCorrelationId();
    }
}

/// <summary>
/// Context for tracking correlation across integration events in a workflow
/// </summary>
public sealed class IntegrationEventCorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets the current correlation ID from the ambient context
    /// </summary>
    public static string? Current => _correlationId.Value;

    /// <summary>
    /// Sets the correlation ID for the current execution context
    /// </summary>
    /// <param name="correlationId">Correlation ID to set</param>
    public static void Set(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    /// <summary>
    /// Clears the correlation ID from the current execution context
    /// </summary>
    public static void Clear()
    {
        _correlationId.Value = null;
    }

    /// <summary>
    /// Creates a scope where the correlation ID is automatically set and cleared
    /// </summary>
    /// <param name="correlationId">Correlation ID for the scope</param>
    /// <returns>Disposable scope that will clear the correlation ID when disposed</returns>
    public static IDisposable CreateScope(string correlationId)
    {
        return new CorrelationScope(correlationId);
    }

    /// <summary>
    /// Creates a scope using the correlation ID from an existing event
    /// </summary>
    /// <param name="integrationEvent">Event to extract correlation ID from</param>
    /// <returns>Disposable scope that will clear the correlation ID when disposed</returns>
    public static IDisposable CreateScope(IIntegrationEvent integrationEvent)
    {
        return CreateScope(integrationEvent.CorrelationId);
    }

    private sealed class CorrelationScope : IDisposable
    {
        private readonly string? _previousCorrelationId;

        public CorrelationScope(string correlationId)
        {
            _previousCorrelationId = Current;
            Set(correlationId);
        }

        public void Dispose()
        {
            if (_previousCorrelationId != null)
                Set(_previousCorrelationId);
            else
                Clear();
        }
    }
}

/// <summary>
/// Registry for tracking event version compatibility and migrations
/// </summary>
public static class IntegrationEventVersionRegistry
{
    private static readonly ConcurrentDictionary<string, EventVersionInfo> _eventVersions = new();

    /// <summary>
    /// Registers version information for an event type
    /// </summary>
    /// <param name="eventType">Type name of the event</param>
    /// <param name="currentVersion">Current version of the event</param>
    /// <param name="supportedVersions">List of versions that are still supported</param>
    /// <param name="migrationFunc">Optional function to migrate between versions</param>
    public static void RegisterEventVersion(
        string eventType, 
        int currentVersion, 
        int[] supportedVersions,
        Func<object, int, int, object>? migrationFunc = null)
    {
        _eventVersions.TryAdd(eventType, new EventVersionInfo(
            currentVersion, 
            supportedVersions, 
            migrationFunc));
    }

    /// <summary>
    /// Checks if a specific version of an event is supported
    /// </summary>
    /// <param name="eventType">Type name of the event</param>
    /// <param name="version">Version to check</param>
    /// <returns>True if the version is supported</returns>
    public static bool IsVersionSupported(string eventType, int version)
    {
        return _eventVersions.TryGetValue(eventType, out var info) && 
               info.SupportedVersions.Contains(version);
    }

    /// <summary>
    /// Gets the current version for an event type
    /// </summary>
    /// <param name="eventType">Type name of the event</param>
    /// <returns>Current version or 1 if not registered</returns>
    public static int GetCurrentVersion(string eventType)
    {
        return _eventVersions.TryGetValue(eventType, out var info) ? info.CurrentVersion : 1;
    }

    private sealed record EventVersionInfo(
        int CurrentVersion,
        int[] SupportedVersions,
        Func<object, int, int, object>? MigrationFunc);
}