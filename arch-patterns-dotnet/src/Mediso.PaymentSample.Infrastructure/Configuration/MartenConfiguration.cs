using System.Diagnostics;
using Marten;
using Marten.Events;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.Infrastructure.EventStore;
using Mediso.PaymentSample.Infrastructure.Monitoring;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.Configuration;

public static class MartenConfiguration
{
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);
    
    public static IServiceCollection AddMartenEventStore(this IServiceCollection services, string connectionString)
    {
        services.AddMarten(options =>
        {
            // Connection configuration with PostgreSQL 18 optimizations
            options.Connection(connectionString);
            
            // Database schema configuration
            options.DatabaseSchemaName = "payment_sample";
            
            // Configure minimal logging for production readiness
            options.Logger(new PostgreSql18Logger());
            
            // Event store configuration
            ConfigureEventStore(options);
        })
        .UseLightweightSessions(); // Use lightweight sessions by default

        // Register custom event store implementation with snapshot support
        services.AddScoped<EventStore.MartenEventStore>();
        services.AddScoped<SharedKernel.Abstractions.IEventStore, EventStore.SnapshotPaymentEventStore>();
        
        // Register PostgreSQL 18 monitoring service
        services.AddScoped<IPostgreSql18MonitoringService, PostgreSql18MonitoringService>();
        
        // Register Payment metrics for OpenTelemetry
        services.AddSingleton<PaymentMetrics>();

        return services;
    }

    private static void ConfigureEventStore(StoreOptions options)
    {
        // Configure event types
        options.Events.AddEventTypes(new[]
        {
            typeof(PaymentRequested),
            typeof(AMLPassed),
            typeof(PaymentFlagged),
            typeof(FundsReserved),
            typeof(FundsReservationFailed),
            typeof(PaymentJournaled),
            typeof(PaymentSettled),
            typeof(PaymentCancelled),
            typeof(PaymentDeclined),
            typeof(PaymentFailed),
            typeof(PaymentNotified)
        });
        
        // Configure Payment aggregate for event sourcing
        
        // Register Payment as a document to enable snapshots
        options.RegisterDocumentType<Payment>();
        options.Schema.For<Payment>().Identity(x => x.Id);
        
        // Register PaymentSnapshot document for storing snapshots
        options.RegisterDocumentType<PaymentSnapshot>();
        options.Schema.For<PaymentSnapshot>().Identity(x => x.Id);
        
        // Enable event metadata with PostgreSQL 18 enhancements
        options.Events.MetadataConfig.EnableAll();
        options.Events.MetadataConfig.HeadersEnabled = true;
        options.Events.MetadataConfig.CausationIdEnabled = true;
        options.Events.MetadataConfig.CorrelationIdEnabled = true;
        
        // Disable verbose console logging in favor of structured logging
        options.DisableNpgsqlLogging = false;
    }
}
