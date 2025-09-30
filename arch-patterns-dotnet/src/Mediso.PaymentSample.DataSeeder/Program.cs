using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Mediso.PaymentSample.DataSeeder.Configuration;
using Mediso.PaymentSample.DataSeeder.Services;
using Mediso.PaymentSample.Infrastructure.Configuration;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Mediso.PaymentSample.DataSeeder;

class Program
{
    private static readonly ActivitySource ActivitySource = new(TracingConstants.ApplicationServiceName);

    static async Task<int> Main(string[] args)
    {
        // Configure Serilog early
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/dataseeder-.log", rollingInterval: RollingInterval.Day)
            .CreateBootstrapLogger();

        try
        {
            Log.Information("🌱 Starting Mediso Payment Sample Data Seeder");
            
            var rootCommand = CreateRootCommand();
            
            var commandLineBuilder = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseExceptionHandler((exception, context) =>
                {
                    Log.Fatal(exception, "💥 Unhandled exception occurred");
                    context.ExitCode = 1;
                });

            var parser = commandLineBuilder.Build();
            return await parser.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("🌱 Mediso Payment Sample Data Seeder - PostgreSQL 18 + Marten Event Store")
        {
            Description = "Database migration and sample data seeding tool for the Payment Sample application"
        };

        // Global options
        var environmentOption = new Option<string>(
            aliases: new[] { "--environment", "-e" },
            description: "Environment (Development, Staging, Production)",
            getDefaultValue: () => "Development");

        var connectionStringOption = new Option<string>(
            aliases: new[] { "--connection", "-c" },
            description: "Database connection string (overrides configuration)");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose logging");

        rootCommand.AddGlobalOption(environmentOption);
        rootCommand.AddGlobalOption(connectionStringOption);
        rootCommand.AddGlobalOption(verboseOption);

        // Commands
        rootCommand.AddCommand(CreateMigrateCommand());
        rootCommand.AddCommand(CreateSeedCommand());
        rootCommand.AddCommand(CreateStatusCommand());
        rootCommand.AddCommand(CreateResetCommand());

        return rootCommand;
    }

    private static Command CreateMigrateCommand()
    {
        var command = new Command("migrate", "🔧 Initialize database schema and apply PostgreSQL 18 enhancements");
        
        command.SetHandler(async () =>
        {
            using var activity = ActivitySource.StartActivity("DataSeeder.Migrate");
            var host = CreateHost();
            
            try
            {
                using var scope = host.Services.CreateScope();
                var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
                await migrationService.InitializeDatabaseAsync(CancellationToken.None);
                
                Log.Information("✅ Database migration completed successfully");
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Log.Error(ex, "❌ Migration failed");
                throw;
            }
        });

        return command;
    }

    private static Command CreateSeedCommand()
    {
        var command = new Command("seed", "🌱 Generate and seed sample data");

        var paymentsOption = new Option<int>(
            aliases: new[] { "--payments", "-p" },
            description: "Number of payments to generate",
            getDefaultValue: () => 100);

        var customersOption = new Option<int>(
            aliases: new[] { "--customers" },
            description: "Number of customers to generate",
            getDefaultValue: () => 50);

        var merchantsOption = new Option<int>(
            aliases: new[] { "--merchants" },
            description: "Number of merchants to generate",
            getDefaultValue: () => 20);

        var timeRangeOption = new Option<int>(
            aliases: new[] { "--timerange", "-t" },
            description: "Time range in months for historical data",
            getDefaultValue: () => 6);

        var realisticOption = new Option<bool>(
            aliases: new[] { "--realistic", "-r" },
            description: "Enable realistic business scenarios (failures, AML flags, etc.)",
            getDefaultValue: () => true);

        var seedOption = new Option<int?>(
            aliases: new[] { "--seed", "-s" },
            description: "Random seed for reproducible data generation");

        command.AddOption(paymentsOption);
        command.AddOption(customersOption);
        command.AddOption(merchantsOption);
        command.AddOption(timeRangeOption);
        command.AddOption(realisticOption);
        command.AddOption(seedOption);

        command.SetHandler(async (payments, customers, merchants, timeRange, realistic, seed) =>
        {
            using var activity = ActivitySource.StartActivity("DataSeeder.Seed");
            var host = CreateHost();
            
            try
            {
                using var scope = host.Services.CreateScope();
                var sampleDataService = scope.ServiceProvider.GetRequiredService<ISampleDataService>();
                
                var settings = new SampleDataSettings
                {
                    PaymentCount = payments,
                    CustomerCount = customers,
                    MerchantCount = merchants,
                    TimeRangeMonths = timeRange,
                    EnableRealisticScenarios = realistic,
                    RandomSeed = seed
                };

                await sampleDataService.SeedAllAsync(settings, CancellationToken.None);
                
                var stats = await sampleDataService.GetSeedingStatisticsAsync(CancellationToken.None);
                
                Log.Information("✅ Sample data seeding completed");
                Log.Information("📊 Statistics: {TotalPayments} payments, {TotalEvents} events", 
                    stats.TotalPayments, stats.TotalEvents);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Log.Error(ex, "❌ Sample data seeding failed");
                throw;
            }
        }, paymentsOption, customersOption, merchantsOption, timeRangeOption, realisticOption, seedOption);

        return command;
    }

    private static Command CreateStatusCommand()
    {
        var command = new Command("status", "📊 Check database health and seeding statistics");

        command.SetHandler(async () =>
        {
            using var activity = ActivitySource.StartActivity("DataSeeder.Status");
            var host = CreateHost();
            
            try
            {
                using var scope = host.Services.CreateScope();
                var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
                var sampleDataService = scope.ServiceProvider.GetRequiredService<ISampleDataService>();

                Log.Information("📊 Checking database health...");
                var health = await migrationService.GetDatabaseHealthAsync(CancellationToken.None);
                
                Log.Information("🎯 Database Health Report:");
                Log.Information("  PostgreSQL Version: {Version}", health.PostgreSqlVersion.Split(' ').FirstOrDefault());
                Log.Information("  Schema Exists: {SchemaExists}", health.SchemaExists ? "✅" : "❌");
                Log.Information("  Monitoring Enabled: {MonitoringEnabled}", health.MonitoringEnabled ? "✅" : "❌");
                Log.Information("  Table Count: {TableCount}", health.TableCount);
                Log.Information("  Event Count: {EventCount:N0}", health.EventCount);
                Log.Information("  Database Size: {DatabaseSize}", health.DatabaseSize);
                Log.Information("  Overall Health: {IsHealthy}", health.IsHealthy ? "✅ Healthy" : "❌ Unhealthy");

                if (health.Issues.Any())
                {
                    Log.Warning("⚠️ Issues found:");
                    foreach (var issue in health.Issues)
                    {
                        Log.Warning("  - {Issue}", issue);
                    }
                }

                if (health.EventCount > 0)
                {
                    var stats = await sampleDataService.GetSeedingStatisticsAsync(CancellationToken.None);
                    
                    Log.Information("🌱 Seeding Statistics:");
                    Log.Information("  Total Payments: {TotalPayments:N0}", stats.TotalPayments);
                    Log.Information("  Total Events: {TotalEvents:N0}", stats.TotalEvents);
                    
                    Log.Information("  Payment Status Distribution:");
                    foreach (var kvp in stats.PaymentStatusCounts)
                    {
                        Log.Information("    {Status}: {Count:N0}", kvp.Key, kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Log.Error(ex, "❌ Status check failed");
                throw;
            }
        });

        return command;
    }

    private static Command CreateResetCommand()
    {
        var command = new Command("reset", "🗑️ Reset database (WARNING: destructive operation)");

        var confirmOption = new Option<bool>(
            aliases: new[] { "--confirm", "-y" },
            description: "Confirm database reset without prompt");

        command.AddOption(confirmOption);

        command.SetHandler(async (confirm) =>
        {
            using var activity = ActivitySource.StartActivity("DataSeeder.Reset");
            
            if (!confirm)
            {
                Log.Warning("⚠️ This will DELETE ALL DATA in the database!");
                Log.Information("Use --confirm flag to proceed without this prompt");
                return;
            }

            var host = CreateHost();
            
            try
            {
                using var scope = host.Services.CreateScope();
                var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
                await migrationService.ResetDatabaseAsync(CancellationToken.None);
                
                Log.Information("✅ Database reset completed successfully");
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Log.Error(ex, "❌ Database reset failed");
                throw;
            }
        }, confirmOption);

        return command;
    }

    private static IHost CreateHost()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var connectionString = Environment.GetEnvironmentVariable("DATASEEDER_ConnectionStrings__Default");
        var verbose = Environment.GetEnvironmentVariable("DATASEEDER_Verbose") == "true";

        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                hostContext.HostingEnvironment.EnvironmentName = environment;
                
                // Get the DataSeeder project directory
                var baseDirectory = AppContext.BaseDirectory;
                var settingsPath = Path.Combine(baseDirectory, "appsettings.json");
                var environmentSettingsPath = Path.Combine(baseDirectory, $"appsettings.{environment}.json");
                
                config.AddJsonFile(settingsPath, optional: false, reloadOnChange: true);
                config.AddJsonFile(environmentSettingsPath, optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables("DATASEEDER_");
                
                if (connectionString != null)
                {
                    config.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("ConnectionStrings:Default", connectionString)
                    });
                }
            })
            .UseSerilog((hostContext, loggerConfig) =>
            {
                loggerConfig.ReadFrom.Configuration(hostContext.Configuration);
                
                if (verbose)
                {
                    loggerConfig.MinimumLevel.Debug();
                }
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Configuration
                services.Configure<DataSeederSettings>(
                    hostContext.Configuration.GetSection(DataSeederSettings.SectionName));
                services.Configure<PostgreSqlSettings>(
                    hostContext.Configuration.GetSection(PostgreSqlSettings.SectionName));
                services.Configure<OpenTelemetrySettings>(
                    hostContext.Configuration.GetSection(OpenTelemetrySettings.SectionName));

                // Infrastructure
                var connectionString = hostContext.Configuration.GetConnectionString("Default")!;
                services.AddMartenEventStore(connectionString);

                // Services
                services.AddScoped<IMigrationService, PostgreSql18MigrationService>();
                services.AddScoped<ISampleDataService, BogusSampleDataService>();

                // Logging
                services.AddLogging();
            })
            .Build();
    }
}