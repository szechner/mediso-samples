using System.Diagnostics;
using JasperFx;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Resources;
using Marten;
using Marten.Events.Projections;
using Mediso.PaymentSample.Application.Modules.Payments.Ports;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.Infrastructure.EventStore;
using Mediso.PaymentSample.Infrastructure.Monitoring;
using Mediso.PaymentSample.Infrastructure.Repositories;
using Mediso.PaymentSample.Infrastructure.Services;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Marten;

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
                
                options.Events.StreamIdentity = StreamIdentity.AsGuid;
                // Configure minimal logging for production readiness
                options.Logger(new PostgreSql18Logger());

                // Event store configuration
                ConfigureEventStore(options);
                
                options.DisableNpgsqlLogging = false;
                options.AutoCreateSchemaObjects = AutoCreate.All;
        
                options.UseNewtonsoftForSerialization();
            })
            // Integrate Marten with Wolverine for durable messaging, inbox/outbox, and saga storage
            .IntegrateWithWolverine(cfg =>
            {
                cfg.UseWolverineManagedEventSubscriptionDistribution = true;
            })
            .AddAsyncDaemon(DaemonMode.HotCold)
            .UseLightweightSessions(); // Use lightweight sessions by default

        services.AddScoped<SharedKernel.Abstractions.IEventStore, MartenEventStore>();

        // Register PostgreSQL 18 monitoring service
        services.AddScoped<IPostgreSql18MonitoringService, PostgreSql18MonitoringService>();

        // Register Payment metrics for OpenTelemetry
        services.AddSingleton<PaymentMetrics>();

        // Register repository implementations
        services.AddScoped<IPaymentRepository, MartenPaymentRepository>();

        // Register infrastructure service implementations
        services.AddScoped<IIdempotencyService, MemoryIdempotencyService>();
        services.AddScoped<IPaymentProcessor, StubPaymentProcessor>();
        services.AddScoped<IPaymentNotificationService, StubPaymentNotificationService>();

        services.AddResourceSetupOnStartup();


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

        options.Schema.For<Payment>().Identity(x =>  x.Id);

        options.Projections.Snapshot<Payment>(SnapshotLifecycle.Inline).Identity(x => x.Id);

        // Enable event metadata with PostgreSQL 18 enhancements
        options.Events.MetadataConfig.EnableAll();
        options.Events.MetadataConfig.HeadersEnabled = true;
        options.Events.MetadataConfig.CausationIdEnabled = true;
        options.Events.MetadataConfig.CorrelationIdEnabled = true;
    }
}