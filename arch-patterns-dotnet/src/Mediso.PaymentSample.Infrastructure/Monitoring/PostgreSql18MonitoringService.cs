using System.Text.Json;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mediso.PaymentSample.Infrastructure.Monitoring;

/// <summary>
/// Service for accessing PostgreSQL 18 enhanced monitoring functions
/// </summary>
public interface IPostgreSql18MonitoringService
{
    Task<EventStoreStatistics> GetEventStoreStatisticsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SlowQueryInfo>> GetSlowQueriesAsync(int minDurationMs = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConnectionStatistic>> GetConnectionStatisticsAsync(CancellationToken cancellationToken = default);
    Task<CacheHitRatio> GetCacheHitRatioAsync(CancellationToken cancellationToken = default);
    Task<string> GetMonitoringDashboardJsonAsync(CancellationToken cancellationToken = default);
}

public class PostgreSql18MonitoringService : IPostgreSql18MonitoringService
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<PostgreSql18MonitoringService> _logger;

    public PostgreSql18MonitoringService(IDocumentStore documentStore, ILogger<PostgreSql18MonitoringService> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
    }

    public async Task<EventStoreStatistics> GetEventStoreStatisticsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _documentStore.Storage.Database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM monitoring.get_event_store_stats()";
        
        using var command = new NpgsqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var statistics = new List<EventStoreTableInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            statistics.Add(new EventStoreTableInfo
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                TotalEvents = reader.GetInt64(2),
                TableSize = reader.GetString(3),
                IndexSize = reader.GetString(4),
                TotalSize = reader.GetString(5)
            });
        }

        _logger.LogInformation("Retrieved event store statistics for {TableCount} tables", statistics.Count);

        return new EventStoreStatistics
        {
            Tables = statistics,
            RetrievedAt = DateTimeOffset.UtcNow,
            PostgreSqlVersion = "18"
        };
    }

    public async Task<IEnumerable<SlowQueryInfo>> GetSlowQueriesAsync(int minDurationMs = 100, CancellationToken cancellationToken = default)
    {
        using var connection = _documentStore.Storage.Database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM monitoring.get_slow_queries($1)";
        
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(minDurationMs);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var slowQueries = new List<SlowQueryInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            slowQueries.Add(new SlowQueryInfo
            {
                QueryHash = reader.GetString(0),
                Query = reader.GetString(1),
                Calls = reader.GetInt64(2),
                TotalExecTime = reader.GetDecimal(3),
                MeanExecTime = reader.GetDecimal(4),
                MaxExecTime = reader.GetDecimal(5),
                RowsReturned = reader.GetInt64(6)
            });
        }

        _logger.LogInformation("Retrieved {SlowQueryCount} slow queries (>{MinDuration}ms)", slowQueries.Count, minDurationMs);

        return slowQueries;
    }

    public async Task<IEnumerable<ConnectionStatistic>> GetConnectionStatisticsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _documentStore.Storage.Database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM monitoring.get_connection_stats()";
        
        using var command = new NpgsqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var connectionStats = new List<ConnectionStatistic>();
        while (await reader.ReadAsync(cancellationToken))
        {
            connectionStats.Add(new ConnectionStatistic
            {
                State = reader.GetString(0),
                Count = reader.GetInt64(1),
                MaxDuration = reader.IsDBNull(2) ? null : reader.GetTimeSpan(2)
            });
        }

        _logger.LogDebug("Retrieved connection statistics for {StateCount} different states", connectionStats.Count);

        return connectionStats;
    }

    public async Task<CacheHitRatio> GetCacheHitRatioAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _documentStore.Storage.Database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM monitoring.get_cache_hit_ratio()";
        
        using var command = new NpgsqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        decimal? bufferCacheHitRatio = null;
        decimal? indexCacheHitRatio = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            var objectType = reader.GetString(0);
            var hitRatio = reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1);

            if (objectType == "Buffer Cache")
                bufferCacheHitRatio = hitRatio;
            else if (objectType == "Index Cache")
                indexCacheHitRatio = hitRatio;
        }

        var result = new CacheHitRatio
        {
            BufferCacheHitRatio = bufferCacheHitRatio,
            IndexCacheHitRatio = indexCacheHitRatio,
            MeasuredAt = DateTimeOffset.UtcNow
        };

        _logger.LogInformation("Cache hit ratios - Buffer: {BufferRatio}%, Index: {IndexRatio}%", 
            bufferCacheHitRatio, indexCacheHitRatio);

        return result;
    }

    public async Task<string> GetMonitoringDashboardJsonAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _documentStore.Storage.Database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT section, data FROM monitoring.event_store_dashboard";
        
        using var command = new NpgsqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var dashboard = new Dictionary<string, object>();
        
        while (await reader.ReadAsync(cancellationToken))
        {
            var section = reader.GetString(0);
            var dataJson = reader.GetString(1);
            
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(dataJson);
                dashboard[section] = data;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse dashboard JSON for section {Section}", section);
                dashboard[section] = dataJson;
            }
        }

        var result = JsonSerializer.Serialize(dashboard, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogDebug("Generated monitoring dashboard JSON with {SectionCount} sections", dashboard.Count);

        return result;
    }
}

// Data models for monitoring results
public record EventStoreStatistics
{
    public IEnumerable<EventStoreTableInfo> Tables { get; init; } = Array.Empty<EventStoreTableInfo>();
    public DateTimeOffset RetrievedAt { get; init; }
    public string PostgreSqlVersion { get; init; } = string.Empty;
}

public record EventStoreTableInfo
{
    public string SchemaName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public long TotalEvents { get; init; }
    public string TableSize { get; init; } = string.Empty;
    public string IndexSize { get; init; } = string.Empty;
    public string TotalSize { get; init; } = string.Empty;
}

public record SlowQueryInfo
{
    public string QueryHash { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public long Calls { get; init; }
    public decimal TotalExecTime { get; init; }
    public decimal MeanExecTime { get; init; }
    public decimal MaxExecTime { get; init; }
    public long RowsReturned { get; init; }
}

public record ConnectionStatistic
{
    public string State { get; init; } = string.Empty;
    public long Count { get; init; }
    public TimeSpan? MaxDuration { get; init; }
}

public record CacheHitRatio
{
    public decimal? BufferCacheHitRatio { get; init; }
    public decimal? IndexCacheHitRatio { get; init; }
    public DateTimeOffset MeasuredAt { get; init; }
}