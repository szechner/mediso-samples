using Mediso.PaymentSample.DataSeeder.Configuration;

namespace Mediso.PaymentSample.DataSeeder.Services;

/// <summary>
/// Service for generating and seeding sample data
/// </summary>
public interface ISampleDataService
{
    /// <summary>
    /// Generate and seed all sample data
    /// </summary>
    Task SeedAllAsync(SampleDataSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate and seed customer data
    /// </summary>
    Task SeedCustomersAsync(int count, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate and seed merchant data
    /// </summary>
    Task SeedMerchantsAsync(int count, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate and seed payment transactions
    /// </summary>
    Task SeedPaymentsAsync(int count, int timeRangeMonths, bool enableRealisticScenarios, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear all existing sample data
    /// </summary>
    Task ClearSampleDataAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get statistics about seeded data
    /// </summary>
    Task<SeedingStatistics> GetSeedingStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the seeded data
/// </summary>
public record SeedingStatistics(
    int TotalPayments,
    int TotalCustomers,
    int TotalMerchants,
    int TotalEvents,
    Dictionary<string, int> PaymentStatusCounts,
    Dictionary<string, int> EventTypeCounts,
    DateTimeOffset DataGeneratedAt,
    TimeSpan SeedingDuration
);