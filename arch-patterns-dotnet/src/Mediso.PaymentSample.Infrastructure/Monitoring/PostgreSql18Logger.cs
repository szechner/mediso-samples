using System.Diagnostics;
using Marten;
using Marten.Services;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mediso.PaymentSample.Infrastructure.Monitoring;

/// <summary>
/// Enhanced PostgreSQL 18 logger with performance monitoring and OpenTelemetry integration
/// </summary>
public class PostgreSql18Logger : IMartenLogger
{
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);
    
    public IMartenSessionLogger StartSession(IQuerySession session)
    {
        // Use a simple logger approach for PostgreSQL 18
        return new PostgreSql18SessionLogger(null);
    }

    public void SchemaChange(string sql)
    {
        // PostgreSQL 18 schema change logging
        using var activity = ActivitySource.StartActivity("PostgreSQL18.SchemaChange");
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.schema_change", true);
    }
}

/// <summary>
/// Enhanced session logger for PostgreSQL 18 with detailed performance monitoring
/// </summary>
public class PostgreSql18SessionLogger : IMartenSessionLogger
{
    private readonly ILogger<PostgreSql18SessionLogger>? _logger;
    private readonly Dictionary<string, (DateTime StartTime, string CommandText)> _activeQueries = new();
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);

    public PostgreSql18SessionLogger(ILogger<PostgreSql18SessionLogger>? logger)
    {
        _logger = logger;
    }

    public void LogSuccess(NpgsqlCommand command)
    {
        var queryId = GetQueryId(command);
        var duration = GetQueryDuration(queryId, out var commandText);
        
        using var activity = ActivitySource.StartActivity("PostgreSQL18.Query.Success");
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.command.type", GetCommandType(command.CommandText));
        activity?.SetTag("postgresql.command.text", command.CommandText);
        activity?.SetTag("postgresql.duration_ms", duration);
        activity?.SetTag("postgresql.parameters", command.Parameters.Count);
        activity?.SetTag("postgresql.command_hash", command.CommandText.GetHashCode());

        if (duration > 100) // Log slow queries
        {
            _logger?.LogWarning(
                "Slow PostgreSQL query detected: {Duration}ms - {CommandType} - Hash: {Hash}",
                duration, GetCommandType(command.CommandText), command.CommandText.GetHashCode());
        }
        else
        {
            _logger?.LogDebug(
                "PostgreSQL query completed: {Duration}ms - {CommandType}",
                duration, GetCommandType(command.CommandText));
        }

        // Enhanced monitoring for event store operations
        if (IsEventStoreOperation(command.CommandText))
        {
            activity?.SetTag("marten.operation_type", "event_store");
            LogEventStoreMetrics(command, duration);
        }
    }

    public void LogFailure(NpgsqlCommand command, Exception ex)
    {
        var queryId = GetQueryId(command);
        var duration = GetQueryDuration(queryId, out var commandText);

        using var activity = ActivitySource.StartActivity("PostgreSQL18.Query.Failure");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.command.type", GetCommandType(command.CommandText));
        activity?.SetTag("postgresql.duration_ms", duration);
        activity?.SetTag("postgresql.error.message", ex.Message);
        activity?.SetTag("postgresql.error.type", ex.GetType().Name);
        activity?.SetTag("postgresql.command_hash", command.CommandText.GetHashCode());
        activity?.SetTag($"postgresql.command..text", command.CommandText);
        
        _logger?.LogError(ex,
            "PostgreSQL query failed after {Duration}ms - {CommandType} - Error: {ErrorType}",
            duration, GetCommandType(command.CommandText), ex.GetType().Name);

        // Log critical event store failures
        if (IsEventStoreOperation(command.CommandText))
        {
            _logger?.LogCritical(ex,
                "EVENT STORE OPERATION FAILED - {Duration}ms - This may affect event consistency!",
                duration);
        }
    }

    public void RecordSavedChanges(IDocumentSession session, IChangeSet commit)
    {
        using var activity = ActivitySource.StartActivity("PostgreSQL18.SaveChanges");
        
        var insertedCount = commit.Inserted.Count();
        var updatedCount = commit.Updated.Count();
        var deletedCount = commit.Deleted.Count();
        var eventsCount = commit.GetEvents().Count();

        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("marten.changes.inserted", insertedCount);
        activity?.SetTag("marten.changes.updated", updatedCount);
        activity?.SetTag("marten.changes.deleted", deletedCount);
        activity?.SetTag("marten.events.count", eventsCount);
        activity?.SetTag("marten.total_changes", insertedCount + updatedCount + deletedCount + eventsCount);

        _logger?.LogInformation(
            "PostgreSQL 18 session changes saved - Inserted: {Inserted}, Updated: {Updated}, Deleted: {Deleted}, Events: {Events}",
            insertedCount, updatedCount, deletedCount, eventsCount);

        // Log event details with enhanced monitoring
        foreach (var evt in commit.GetEvents())
        {
            activity?.AddEvent(new ActivityEvent("PostgreSQL18.EventAppended", DateTimeOffset.UtcNow, new ActivityTagsCollection
            {
                ["event.type"] = evt.EventType.Name,
                ["event.id"] = evt.Id.ToString(),
                ["event.stream_id"] = evt.StreamId,
                ["event.version"] = evt.Version.ToString(),
                ["postgresql.version"] = "18"
            }));

            _logger?.LogDebug(
                "PostgreSQL 18 - Event appended: {EventType} ID:{EventId} Stream:{StreamId} Version:{Version}",
                evt.EventType.Name, evt.Id, evt.StreamId, evt.Version);
        }
    }

    public void OnBeforeExecute(NpgsqlCommand command)
    {
        var queryId = GetQueryId(command);
        _activeQueries[queryId] = (DateTime.UtcNow, command.CommandText);

        using var activity = ActivitySource.StartActivity("PostgreSQL18.Query.BeforeExecute");
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.command.type", GetCommandType(command.CommandText));
        activity?.SetTag("postgresql.command.text", command.CommandText);
        activity?.SetTag("postgresql.parameters", command.Parameters.Count);
        activity?.SetTag("postgresql.query_id", queryId);

        if (IsEventStoreOperation(command.CommandText))
        {
            activity?.SetTag("marten.operation_type", "event_store");
            _logger?.LogDebug("PostgreSQL 18 - Preparing event store operation: {CommandType}", 
                GetCommandType(command.CommandText));
        }
    }

    // Batch operation methods for PostgreSQL 18
    public void LogSuccess(NpgsqlBatch batch)
    {
        using var activity = ActivitySource.StartActivity("PostgreSQL18.Batch.Success");
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.batch.commands", batch.BatchCommands.Count);

        for (var i = 0; i < batch.BatchCommands.Count; i++)
        {
            var batchBatchCommand = batch.BatchCommands[i];
            activity?.SetTag($"postgresql.batch.commands.{i}.text", batchBatchCommand.CommandText);
        }

        _logger?.LogDebug(
            "PostgreSQL 18 batch completed successfully: {CommandCount} commands",
            batch.BatchCommands.Count);
    }

    public void LogFailure(NpgsqlBatch batch, Exception ex)
    {
        using var activity = ActivitySource.StartActivity("PostgreSQL18.Batch.Failure");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.batch.commands", batch.BatchCommands.Count);
        activity?.SetTag("postgresql.error.message", ex.Message);
        
        for (var i = 0; i < batch.BatchCommands.Count; i++)
        {
            var batchBatchCommand = batch.BatchCommands[i];
            activity?.SetTag($"postgresql.batch.commands.{i}.text", batchBatchCommand.CommandText);
        }
        
        _logger?.LogError(ex,
            "PostgreSQL 18 batch failed: {CommandCount} commands - Error: {ErrorType}",
            batch.BatchCommands.Count, ex.GetType().Name);
    }

    public void LogFailure(Exception ex, string message)
    {
        using var activity = ActivitySource.StartActivity("PostgreSQL18.GeneralFailure");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.error.message", ex.Message);
        
        _logger?.LogError(ex, "PostgreSQL 18 general failure: {Message}", message);
    }

    public void OnBeforeExecute(NpgsqlBatch batch)
    {
        using var activity = ActivitySource.StartActivity("PostgreSQL18.Batch.BeforeExecute");
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("postgresql.batch.commands", batch.BatchCommands.Count);
        
        for (var i = 0; i < batch.BatchCommands.Count; i++)
        {
            var batchBatchCommand = batch.BatchCommands[i];
            activity?.SetTag($"postgresql.batch.commands.{i}.text", batchBatchCommand.CommandText);
        }
        
        _logger?.LogDebug(
            "PostgreSQL 18 - Preparing batch execution: {CommandCount} commands",
            batch.BatchCommands.Count);
    }

    private string GetQueryId(NpgsqlCommand command)
    {
        return $"{Thread.CurrentThread.ManagedThreadId}_{command.GetHashCode()}";
    }

    private double GetQueryDuration(string queryId, out string commandText)
    {
        if (_activeQueries.TryGetValue(queryId, out var queryInfo))
        {
            _activeQueries.Remove(queryId);
            commandText = queryInfo.CommandText;
            return (DateTime.UtcNow - queryInfo.StartTime).TotalMilliseconds;
        }
        
        commandText = string.Empty;
        return 0;
    }

    private static string GetCommandType(string commandText)
    {
        if (string.IsNullOrEmpty(commandText)) return "UNKNOWN";
        
        var command = commandText.TrimStart().Split(' ')[0].ToUpperInvariant();
        return command switch
        {
            "SELECT" => "SELECT",
            "INSERT" => "INSERT", 
            "UPDATE" => "UPDATE",
            "DELETE" => "DELETE",
            "CREATE" => "CREATE",
            "ALTER" => "ALTER",
            "DROP" => "DROP",
            _ => "OTHER"
        };
    }

    private static bool IsEventStoreOperation(string commandText)
    {
        if (string.IsNullOrEmpty(commandText)) return false;
        
        var upperCommand = commandText.ToUpperInvariant();
        return upperCommand.Contains("MT_EVENTS") || 
               upperCommand.Contains("MT_STREAMS") ||
               upperCommand.Contains("PAYMENT_SAMPLE");
    }

    private void LogEventStoreMetrics(NpgsqlCommand command, double duration)
    {
        var commandType = GetCommandType(command.CommandText);
        
        _logger?.LogInformation(
            "PostgreSQL 18 Event Store Metrics - Operation: {Operation}, Duration: {Duration}ms, Parameters: {ParameterCount}",
            commandType, duration, command.Parameters.Count);

        // Log performance warnings for event store operations
        if (duration > 500)
        {
            _logger?.LogWarning(
                "PERFORMANCE WARNING: Event store {Operation} took {Duration}ms - Consider optimization",
                commandType, duration);
        }
    }
}