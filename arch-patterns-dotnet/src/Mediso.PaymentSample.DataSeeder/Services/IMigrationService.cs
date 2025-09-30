using Mediso.PaymentSample.DataSeeder.Configuration;

namespace Mediso.PaymentSample.DataSeeder.Services;

/// <summary>
/// Service for handling database migrations and schema setup
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Initialize database schema and Marten event store
    /// </summary>
    Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reset database (WARNING: destructive operation)
    /// </summary>
    Task ResetDatabaseAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if database exists and is properly configured
    /// </summary>
    Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Apply PostgreSQL 18 monitoring functions and optimizations
    /// </summary>
    Task ApplyPostgreSql18EnhancementsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get database statistics and health information
    /// </summary>
    Task<DatabaseHealth> GetDatabaseHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Database health information
/// </summary>
public record DatabaseHealth(
    bool IsHealthy,
    string PostgreSqlVersion,
    bool SchemaExists,
    bool MonitoringEnabled,
    int TableCount,
    int EventCount,
    string DatabaseSize,
    List<string> Issues
);