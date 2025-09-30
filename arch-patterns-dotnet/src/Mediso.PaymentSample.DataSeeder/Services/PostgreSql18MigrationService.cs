using System.Diagnostics;
using Marten;
using Mediso.PaymentSample.DataSeeder.Configuration;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Mediso.PaymentSample.DataSeeder.Services;

/// <summary>
/// PostgreSQL 18 specific migration service with enhanced monitoring
/// </summary>
public class PostgreSql18MigrationService : IMigrationService
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<PostgreSql18MigrationService> _logger;
    private readonly MigrationSettings _migrationSettings;
    private readonly string _connectionString;
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);

    public PostgreSql18MigrationService(
        IDocumentStore documentStore,
        ILogger<PostgreSql18MigrationService> logger,
        IOptions<DataSeederSettings> settings,
        IConfiguration configuration)
    {
        _documentStore = documentStore;
        _logger = logger;
        _migrationSettings = settings.Value.MigrationSettings;
        _connectionString = configuration.GetConnectionString("Default") 
            ?? throw new InvalidOperationException("Default connection string is not configured");
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Migration.InitializeDatabase");
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("migration.operation", "initialize");

        try
        {
            _logger.LogInformation("🔧 Initializing PostgreSQL 18 database with Marten event store...");

            if (_migrationSettings.AutoCreateDatabase)
            {
                await EnsureDatabaseExistsAsync(cancellationToken);
            }

            if (_migrationSettings.AutoCreateSchema)
            {
                _logger.LogInformation("📋 Creating Marten schema objects...");
                await _documentStore.Advanced.Clean.CompletelyRemoveAllAsync(cancellationToken);
                await _documentStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            }

            await ApplyPostgreSql18EnhancementsAsync(cancellationToken);

            _logger.LogInformation("✅ Database initialization completed successfully");
            activity?.SetTag("migration.status", "success");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "❌ Failed to initialize database");
            throw;
        }
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Migration.ResetDatabase");
        activity?.SetTag("postgresql.version", "18");
        activity?.SetTag("migration.operation", "reset");

        if (!_migrationSettings.ResetDatabase)
        {
            _logger.LogWarning("⚠️ Database reset is disabled in configuration");
            return;
        }

        try
        {
            _logger.LogWarning("🗑️ RESETTING DATABASE - This will delete all data!");

            await _documentStore.Advanced.Clean.CompletelyRemoveAllAsync(cancellationToken);
            _logger.LogInformation("🧹 All schema objects removed");

            await InitializeDatabaseAsync(cancellationToken);

            _logger.LogInformation("✅ Database reset completed");
            activity?.SetTag("migration.status", "success");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "❌ Failed to reset database");
            throw;
        }
    }

    public async Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Migration.CheckDatabaseReady");
        
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if database exists and Marten tables are present
            const string checkSql = @"
                SELECT COUNT(*) 
                FROM information_schema.tables 
                WHERE table_schema = 'payment_sample' 
                AND table_name IN ('mt_events', 'mt_streams')";

            using var command = new NpgsqlCommand(checkSql, connection);
            var tableCount = await command.ExecuteScalarAsync(cancellationToken);

            var isReady = Convert.ToInt32(tableCount) >= 2;
            
            activity?.SetTag("database.ready", isReady);
            activity?.SetTag("marten.tables_found", Convert.ToInt32(tableCount));

            return isReady;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to check database readiness");
            return false;
        }
    }

    public async Task ApplyPostgreSql18EnhancementsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Migration.ApplyPostgreSQL18Enhancements");
        
        try
        {
            _logger.LogInformation("🚀 Applying PostgreSQL 18 enhancements...");

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Read and execute the PostgreSQL 18 initialization script
            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "pg18-enhancements.sql");
            
            if (File.Exists(scriptPath))
            {
                var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
                using var command = new NpgsqlCommand(script, connection);
                command.CommandTimeout = _migrationSettings.MigrationTimeoutSeconds;
                
                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("📊 PostgreSQL 18 monitoring functions applied");
            }
            else
            {
                await ApplyBuiltInEnhancementsAsync(connection, cancellationToken);
            }

            activity?.SetTag("postgresql18.enhancements_applied", true);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to apply PostgreSQL 18 enhancements");
            throw;
        }
    }

    public async Task<DatabaseHealth> GetDatabaseHealthAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Migration.GetDatabaseHealth");
        
        var issues = new List<string>();
        
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Get PostgreSQL version
            var versionSql = "SELECT version()";
            using var versionCommand = new NpgsqlCommand(versionSql, connection);
            var version = await versionCommand.ExecuteScalarAsync(cancellationToken) as string ?? "Unknown";

            // Check schema exists
            var schemaSql = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'payment_sample'";
            using var schemaCommand = new NpgsqlCommand(schemaSql, connection);
            var schemaExists = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken)) > 0;

            // Check monitoring enabled
            var monitoringSql = "SELECT COUNT(*) FROM information_schema.routines WHERE routine_schema = 'monitoring'";
            using var monitoringCommand = new NpgsqlCommand(monitoringSql, connection);
            var monitoringEnabled = Convert.ToInt32(await monitoringCommand.ExecuteScalarAsync(cancellationToken)) > 0;

            // Get table count
            var tableCountSql = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'payment_sample'";
            using var tableCommand = new NpgsqlCommand(tableCountSql, connection);
            var tableCount = Convert.ToInt32(await tableCommand.ExecuteScalarAsync(cancellationToken));

            // Get event count (if events table exists)
            var eventCount = 0;
            try
            {
                var eventCountSql = "SELECT COUNT(*) FROM payment_sample.mt_events";
                using var eventCommand = new NpgsqlCommand(eventCountSql, connection);
                eventCount = Convert.ToInt32(await eventCommand.ExecuteScalarAsync(cancellationToken));
            }
            catch
            {
                issues.Add("Events table not accessible");
            }

            // Get database size
            var sizeSql = "SELECT pg_size_pretty(pg_database_size(current_database()))";
            using var sizeCommand = new NpgsqlCommand(sizeSql, connection);
            var databaseSize = await sizeCommand.ExecuteScalarAsync(cancellationToken) as string ?? "Unknown";

            var isHealthy = schemaExists && tableCount > 0 && issues.Count == 0;

            return new DatabaseHealth(
                isHealthy,
                version,
                schemaExists,
                monitoringEnabled,
                tableCount,
                eventCount,
                databaseSize,
                issues
            );
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            issues.Add($"Database health check failed: {ex.Message}");
            
            return new DatabaseHealth(
                false,
                "Unknown",
                false,
                false,
                0,
                0,
                "Unknown",
                issues
            );
        }
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to connect to the target database
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            _logger.LogDebug("Database already exists and is accessible");
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000") // database does not exist
        {
            _logger.LogInformation("📦 Creating database as it doesn't exist...");
            
            // Connect to postgres database to create the target database
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.Database;
            builder.Database = "postgres";
            
            using var connection = new NpgsqlConnection(builder.ToString());
            await connection.OpenAsync(cancellationToken);
            
            using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            _logger.LogInformation("✅ Database created successfully");
        }
    }

    private async Task ApplyBuiltInEnhancementsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"
            -- Create monitoring schema if it doesn't exist
            CREATE SCHEMA IF NOT EXISTS monitoring;

            -- Enable pg_stat_statements if available
            CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

            -- Grant permissions to payment_user
            GRANT USAGE ON SCHEMA monitoring TO payment_user;
            GRANT ALL PRIVILEGES ON SCHEMA monitoring TO payment_user;

            -- Log completion
            DO $$ BEGIN 
                RAISE NOTICE 'Built-in PostgreSQL 18 enhancements applied successfully';
            END $$;
        ";

        using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = _migrationSettings.MigrationTimeoutSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("📊 Built-in PostgreSQL 18 enhancements applied");
    }
}