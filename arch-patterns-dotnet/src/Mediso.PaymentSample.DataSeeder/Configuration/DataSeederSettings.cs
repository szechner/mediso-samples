namespace Mediso.PaymentSample.DataSeeder.Configuration;

/// <summary>
/// Configuration settings for the DataSeeder application
/// </summary>
public class DataSeederSettings
{
    public const string SectionName = "DataSeeder";
    
    public string Environment { get; set; } = "Development";
    public SampleDataSettings SampleDataSettings { get; set; } = new();
    public MigrationSettings MigrationSettings { get; set; } = new();
}

/// <summary>
/// Settings for generating sample data
/// </summary>
public class SampleDataSettings
{
    /// <summary>
    /// Number of sample payments to generate
    /// </summary>
    public int PaymentCount { get; set; } = 100;
    
    /// <summary>
    /// Number of sample customers to generate
    /// </summary>
    public int CustomerCount { get; set; } = 50;
    
    /// <summary>
    /// Number of sample merchants to generate
    /// </summary>
    public int MerchantCount { get; set; } = 20;
    
    /// <summary>
    /// Time range in months for generating historical data
    /// </summary>
    public int TimeRangeMonths { get; set; } = 6;
    
    /// <summary>
    /// Enable realistic business scenarios (failed payments, AML flags, etc.)
    /// </summary>
    public bool EnableRealisticScenarios { get; set; } = true;
    
    /// <summary>
    /// Simulate network latency for testing purposes
    /// </summary>
    public bool SimulateLatency { get; set; } = false;
    
    /// <summary>
    /// Seed for random data generation (for reproducible datasets)
    /// </summary>
    public int? RandomSeed { get; set; }
}

/// <summary>
/// Settings for database migrations and schema setup
/// </summary>
public class MigrationSettings
{
    /// <summary>
    /// Automatically create database if it doesn't exist
    /// </summary>
    public bool AutoCreateDatabase { get; set; } = true;
    
    /// <summary>
    /// Automatically create schema objects
    /// </summary>
    public bool AutoCreateSchema { get; set; } = true;
    
    /// <summary>
    /// Reset database before seeding (WARNING: destructive)
    /// </summary>
    public bool ResetDatabase { get; set; } = false;
    
    /// <summary>
    /// Enable detailed migration logging
    /// </summary>
    public bool EnableMigrationLogging { get; set; } = true;
    
    /// <summary>
    /// Timeout for migration operations in seconds
    /// </summary>
    public int MigrationTimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// PostgreSQL specific configuration
/// </summary>
public class PostgreSqlSettings
{
    public const string SectionName = "PostgreSQL";
    
    public string Version { get; set; } = "18";
    public bool EnableStatistics { get; set; } = true;
    public bool EnableMonitoring { get; set; } = true;
    public int CommandTimeout { get; set; } = 30;
}

/// <summary>
/// OpenTelemetry configuration for tracing data seeding operations
/// </summary>
public class OpenTelemetrySettings
{
    public const string SectionName = "OpenTelemetry";
    
    public string ServiceName { get; set; } = "Mediso.PaymentSample.DataSeeder";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string JaegerEndpoint { get; set; } = "http://localhost:14268/api/traces";
}