using System.Diagnostics;
using Bogus;
using Marten;
using Mediso.PaymentSample.DataSeeder.Configuration;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.Infrastructure.EventStore;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.DataSeeder.Services;

/// <summary>
/// Sample data service using Bogus for realistic fake data generation
/// </summary>
public class BogusSampleDataService : ISampleDataService
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<BogusSampleDataService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private static readonly ActivitySource ActivitySource = new(TracingConstants.ApplicationServiceName);

    // Data storage for referential integrity
    private readonly List<CustomerInfo> _customers = new();
    private readonly List<MerchantInfo> _merchants = new();
    private readonly List<PaymentId> _paymentIds = new();

    private readonly Faker _faker;

    public BogusSampleDataService(IDocumentStore documentStore, ILogger<BogusSampleDataService> logger, ILoggerFactory loggerFactory)
    {
        _documentStore = documentStore;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _faker = new Faker("en");
    }

    public async Task SeedAllAsync(SampleDataSettings settings, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SampleData.SeedAll");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (settings.RandomSeed.HasValue)
            {
                Randomizer.Seed = new Random(settings.RandomSeed.Value);
            }

            activity?.SetTag("seeding.payment_count", settings.PaymentCount);
            activity?.SetTag("seeding.customer_count", settings.CustomerCount);
            activity?.SetTag("seeding.merchant_count", settings.MerchantCount);
            activity?.SetTag("seeding.realistic_scenarios", settings.EnableRealisticScenarios);

            _logger.LogInformation("🌱 Starting comprehensive data seeding...");

            // Clear existing data
            _customers.Clear();
            _merchants.Clear();
            _paymentIds.Clear();

            // Seed customers first
            await SeedCustomersAsync(settings.CustomerCount, cancellationToken);

            // Seed merchants
            await SeedMerchantsAsync(settings.MerchantCount, cancellationToken);

            // Seed payments with realistic scenarios
            await SeedPaymentsAsync(
                settings.PaymentCount, 
                settings.TimeRangeMonths, 
                settings.EnableRealisticScenarios, 
                cancellationToken);

            stopwatch.Stop();
            
            var statistics = await GetSeedingStatisticsAsync(cancellationToken);
            
            activity?.SetTag("seeding.total_events", statistics.TotalEvents);
            activity?.SetTag("seeding.duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("✅ Data seeding completed in {Duration:F2}s", stopwatch.Elapsed.TotalSeconds);
            _logger.LogInformation("📊 Generated: {Payments} payments, {Events} events", 
                statistics.TotalPayments, statistics.TotalEvents);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "❌ Failed to seed sample data");
            throw;
        }
    }

    public Task SeedCustomersAsync(int count, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SampleData.SeedCustomers");
        activity?.SetTag("seeding.customer_count", count);

        _logger.LogInformation("👥 Generating {Count} customers...", count);

        var customerFaker = new Faker<CustomerInfo>()
            .RuleFor(c => c.Id, f => AccountId.New(f.Random.Guid().ToString()))
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.Email, f => f.Person.Email)
            .RuleFor(c => c.Country, f => f.Address.CountryCode())
            .RuleFor(c => c.IsHighRisk, f => f.Random.Bool(0.1f)) // 10% high risk
            .RuleFor(c => c.CreatedAt, f => f.Date.Between(DateTime.UtcNow.AddMonths(-12), DateTime.UtcNow));

        var customers = customerFaker.Generate(count);
        _customers.AddRange(customers);

        _logger.LogInformation("✅ Generated {Count} customers", customers.Count);
        
        if (customers.Any(c => c.IsHighRisk))
        {
            _logger.LogInformation("⚠️ {HighRiskCount} customers marked as high-risk", 
                customers.Count(c => c.IsHighRisk));
        }
        
        return Task.CompletedTask;
    }

    public Task SeedMerchantsAsync(int count, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SampleData.SeedMerchants");
        activity?.SetTag("seeding.merchant_count", count);

        _logger.LogInformation("🏪 Generating {Count} merchants...", count);

        var merchantFaker = new Faker<MerchantInfo>()
            .RuleFor(m => m.Id, f => AccountId.New(f.Random.Guid().ToString()))
            .RuleFor(m => m.Name, f => f.Company.CompanyName())
            .RuleFor(m => m.BusinessType, f => f.PickRandom("E-commerce", "Retail", "SaaS", "Marketplace", "Gaming", "Fintech"))
            .RuleFor(m => m.Country, f => f.Address.CountryCode())
            .RuleFor(m => m.IsHighVolume, f => f.Random.Bool(0.2f)) // 20% high volume
            .RuleFor(m => m.CreatedAt, f => f.Date.Between(DateTime.UtcNow.AddMonths(-24), DateTime.UtcNow));

        var merchants = merchantFaker.Generate(count);
        _merchants.AddRange(merchants);

        _logger.LogInformation("✅ Generated {Count} merchants", merchants.Count);
        
        if (merchants.Any(m => m.IsHighVolume))
        {
            _logger.LogInformation("📈 {HighVolumeCount} merchants marked as high-volume", 
                merchants.Count(m => m.IsHighVolume));
        }
        
        return Task.CompletedTask;
    }

    public async Task SeedPaymentsAsync(int count, int timeRangeMonths, bool enableRealisticScenarios, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SampleData.SeedPayments");
        activity?.SetTag("seeding.payment_count", count);
        activity?.SetTag("seeding.time_range_months", timeRangeMonths);
        activity?.SetTag("seeding.realistic_scenarios", enableRealisticScenarios);

        _logger.LogInformation("💳 Generating {Count} payments over {Months} months...", count, timeRangeMonths);

        if (!_customers.Any() || !_merchants.Any())
        {
            throw new InvalidOperationException("Customers and merchants must be seeded before payments");
        }

        var currencies = new[] { "USD", "EUR", "GBP", "CZK", "JPY" };
        var paymentReferences = new[] { "Online Purchase", "Subscription", "Marketplace", "Gaming", "Transfer", "Refund" };

        var batchSize = Math.Max(1, Math.Min(50, count / 10)); // Process in batches, minimum 1
        var batches = (count + batchSize - 1) / batchSize;

        for (int batchIndex = 0; batchIndex < batches; batchIndex++)
        {
            var currentBatchSize = Math.Min(batchSize, count - (batchIndex * batchSize));
            
            _logger.LogDebug("Processing batch {BatchIndex}/{TotalBatches} ({BatchSize} payments)", 
                batchIndex + 1, batches, currentBatchSize);

            var paymentTasks = new List<Task>();

            for (int i = 0; i < currentBatchSize; i++)
            {
                var task = Task.Run(async () =>
                {
                    // Create independent document session and event store for this task
                    using var session = _documentStore.LightweightSession();
                    using var eventStore = new MartenEventStore(session, _loggerFactory.CreateLogger<MartenEventStore>());
                    
                    await GeneratePaymentScenarioAsync(eventStore, timeRangeMonths, enableRealisticScenarios, currencies, paymentReferences, cancellationToken);
                }, cancellationToken);
                paymentTasks.Add(task);
            }

            await Task.WhenAll(paymentTasks);

            // Small delay between batches to avoid overwhelming the system
            if (batchIndex < batches - 1)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        _logger.LogInformation("✅ Generated {Count} payment scenarios", count);
    }

    public Task ClearSampleDataAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SampleData.ClearAll");

        _logger.LogInformation("🧹 Clearing all sample data...");

        // Clear in-memory collections
        _customers.Clear();
        _merchants.Clear();
        _paymentIds.Clear();

        // Note: In a real scenario, you might want to clear the event store
        // However, with Marten event sourcing, you typically wouldn't delete events
        _logger.LogInformation("✅ Sample data collections cleared");
        
        return Task.CompletedTask;
    }

    public Task<SeedingStatistics> GetSeedingStatisticsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SampleData.GetStatistics");

        try
        {
            // In a real implementation, you would query the event store for actual statistics
            // For now, we'll return basic statistics
            var paymentStatusCounts = new Dictionary<string, int>
            {
                { "Completed", _paymentIds.Count * 70 / 100 },
                { "Failed", _paymentIds.Count * 15 / 100 },
                { "Pending", _paymentIds.Count * 10 / 100 },
                { "Cancelled", _paymentIds.Count * 5 / 100 }
            };

            var eventTypeCounts = new Dictionary<string, int>
            {
                { "PaymentRequested", _paymentIds.Count },
                { "PaymentSettled", _paymentIds.Count * 70 / 100 },
                { "PaymentFailed", _paymentIds.Count * 15 / 100 },
                { "FundsReserved", _paymentIds.Count * 85 / 100 },
                { "AMLPassed", _paymentIds.Count * 95 / 100 }
            };

            var totalEvents = eventTypeCounts.Values.Sum();

            return Task.FromResult(new SeedingStatistics(
                TotalPayments: _paymentIds.Count,
                TotalCustomers: _customers.Count,
                TotalMerchants: _merchants.Count,
                TotalEvents: totalEvents,
                PaymentStatusCounts: paymentStatusCounts,
                EventTypeCounts: eventTypeCounts,
                DataGeneratedAt: DateTimeOffset.UtcNow,
                SeedingDuration: TimeSpan.Zero // Would be calculated from actual seeding time
            ));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to get seeding statistics");
            throw;
        }
    }

    private async Task GeneratePaymentScenarioAsync(
        IEventStore eventStore,
        int timeRangeMonths, 
        bool enableRealisticScenarios, 
        string[] currencies, 
        string[] paymentReferences,
        CancellationToken cancellationToken)
    {
        var paymentId = PaymentId.New();
        _paymentIds.Add(paymentId);

        var customer = _faker.PickRandom(_customers);
        var merchant = _faker.PickRandom(_merchants);
        var currency = new Currency(_faker.PickRandom(currencies));
        var reference = _faker.PickRandom(paymentReferences);

        // Generate realistic amounts based on currency
        var amount = currency.Code switch
        {
            "USD" or "EUR" or "GBP" => _faker.Random.Decimal(10, 5000),
            "CZK" => _faker.Random.Decimal(250, 125000),
            "JPY" => _faker.Random.Decimal(1000, 500000),
            _ => _faker.Random.Decimal(10, 1000)
        };

        var money = new Money(Math.Round(amount, 2), currency);
        var createdAt = _faker.Date.Between(DateTime.UtcNow.AddMonths(-timeRangeMonths), DateTime.UtcNow);

        // Create initial payment requested event
        var events = new List<IDomainEvent>
        {
            new PaymentRequested(paymentId, money, customer.Id, merchant.Id, reference)
            {
                CreatedAt = createdAt
            }
        };

        if (enableRealisticScenarios)
        {
            // Generate realistic payment lifecycle events
            await GenerateRealisticPaymentLifecycleAsync(paymentId, money, customer, merchant, createdAt, events);
        }
        else
        {
            // Simple successful scenario
            events.Add(new AMLPassed(paymentId, "v2.1") { CreatedAt = createdAt.AddSeconds(1) });
            events.Add(new FundsReserved(paymentId, ReservationId.New(), money) { CreatedAt = createdAt.AddSeconds(2) });
            events.Add(new PaymentSettled(paymentId, "API", null) { CreatedAt = createdAt.AddSeconds(3) });
        }

        // Append events to the event store
        try
        {
            // Use -1 for new streams in Marten (indicates no existing events)
            await eventStore.AppendEventsAsync(paymentId, -1, events, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append events for payment {PaymentId}", paymentId);
        }
    }

    private Task GenerateRealisticPaymentLifecycleAsync(
        PaymentId paymentId, 
        Money money, 
        CustomerInfo customer, 
        MerchantInfo merchant, 
        DateTime createdAt, 
        List<IDomainEvent> events)
    {
        var currentTime = createdAt.AddSeconds(1);

        // AML Check - higher failure rate for high-risk customers
        if (customer.IsHighRisk && _faker.Random.Bool(0.3f))
        {
            events.Add(new PaymentFlagged(paymentId, "High-risk customer profile", "HIGH") { CreatedAt = currentTime });
            events.Add(new PaymentDeclined(paymentId, "AML check failed") { CreatedAt = currentTime.AddSeconds(1) });
            return Task.CompletedTask;
        }

        events.Add(new AMLPassed(paymentId, "v2.1") { CreatedAt = currentTime });
        currentTime = currentTime.AddSeconds(_faker.Random.Int(1, 5));

        // Funds Reservation - higher failure rate for large amounts
        var highAmountThreshold = money.Currency.Code switch
        {
            "USD" or "EUR" or "GBP" => 1000m,
            "CZK" => 25000m,
            "JPY" => 100000m,
            _ => 500m
        };

        if (money.Amount > highAmountThreshold && _faker.Random.Bool(0.2f))
        {
            events.Add(new FundsReservationFailed(paymentId, "Insufficient funds") { CreatedAt = currentTime });
            events.Add(new PaymentFailed(paymentId, "Funds reservation failed") { CreatedAt = currentTime.AddSeconds(1) });
            return Task.CompletedTask;
        }

        var reservationId = ReservationId.New();
        events.Add(new FundsReserved(paymentId, reservationId, money) { CreatedAt = currentTime });
        currentTime = currentTime.AddSeconds(_faker.Random.Int(1, 10));

        // Payment processing scenarios
        var scenario = _faker.Random.Float();
        
        if (scenario < 0.75f) // 75% successful
        {
            // Successful payment flow
            var ledgerEntries = new List<LedgerEntry>
            {
                new(LedgerEntryId.New(), customer.Id, merchant.Id, money)
            };
            
            events.Add(new PaymentJournaled(paymentId, ledgerEntries) { CreatedAt = currentTime });
            currentTime = currentTime.AddSeconds(_faker.Random.Int(1, 3));
            
            var channel = _faker.PickRandom("API", "Web", "Mobile", "Batch");
            var externalRef = _faker.Random.AlphaNumeric(10);
            events.Add(new PaymentSettled(paymentId, channel, externalRef) { CreatedAt = currentTime });
            
            // Optional notification
            if (_faker.Random.Bool(0.8f))
            {
                events.Add(new PaymentNotified(paymentId, "Email") { CreatedAt = currentTime.AddSeconds(5) });
            }
        }
        else if (scenario < 0.9f) // 15% failed
        {
            var reasons = new[] { "Network timeout", "Processing error", "Invalid account", "Service unavailable" };
            var reason = _faker.PickRandom(reasons);
            events.Add(new PaymentFailed(paymentId, reason) { CreatedAt = currentTime });
        }
        else // 10% cancelled
        {
            var cancelledBy = _faker.PickRandom("Customer", "System", "Merchant", "Admin");
            events.Add(new PaymentCancelled(paymentId, cancelledBy) { CreatedAt = currentTime });
        }
        
        return Task.CompletedTask;
    }

    private class CustomerInfo
    {
        public AccountId Id { get; set; } = AccountId.New(Guid.NewGuid().ToString());
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public bool IsHighRisk { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    
    private class MerchantInfo
    {
        public AccountId Id { get; set; } = AccountId.New(Guid.NewGuid().ToString());
        public string Name { get; set; } = string.Empty;
        public string BusinessType { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public bool IsHighVolume { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}