using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Mediso.PaymentSample.Application.Modules.Payments.UseCases;
using Mediso.PaymentSample.Application.Modules.Payments.Ports.Primary;
using Mediso.PaymentSample.Application.Common.Resilience;
using FluentValidation;
using System.Reflection;
using JasperFx.Resources;
using Mediso.PaymentSample.Application.Common;
using Mediso.PaymentSample.Application.Modules.FraudDetection.Handlers;
using Mediso.PaymentSample.Application.Modules.Payments.Handlers;
using Mediso.PaymentSample.Application.Modules.Payments.Sagas;
using Mediso.PaymentSample.SharedKernel.Modules;
using Wolverine.ErrorHandling;
using Wolverine.RDBMS;

namespace Mediso.PaymentSample.Application.Configuration;

public static class ApplicationConfiguration
{
    /// <summary>
    /// Configures application services including Wolverine, and use cases.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add FluentValidation
        services.AddValidatorsFromAssembly(typeof(ApplicationConfiguration).Assembly);
        
        // Add resilience pipeline provider
            services.AddSingleton<IResiliencePipelineProvider, PaymentResiliencePipelineProvider>();
        
        // Register payment query handlers
        services.AddScoped<_PaymentQueryHandlers>();
        
        // Register fraud detection services
        services.AddScoped<IFraudDetectionService, MockFraudDetectionService>();
            
        services.AddScoped<IInitiatePaymentHandler, InitiatePaymentCommandHandler>();
        services.AddScoped<IReservePaymentHandler, ReservePaymentCommandHandler>();
        services.AddScoped<ISettlePaymentHandler, SettlePaymentCommandHandler>();
        services.AddScoped<ICancelPaymentHandler, CancelPaymentCommandHandler>();

        services.AddHttpContextAccessor();
        return services;
    }

    /// <summary>
    /// Configures Wolverine with Marten integration for the payment sample.
    /// This method should be called AFTER Marten has been configured with IntegrateWithWolverine().
    /// </summary>
    public static IHostBuilder UseWolverineWithMarten(this IHostBuilder builder)
    {
        return builder.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(ApplicationConfiguration).Assembly);
            
            // Configure local queues for payment processing with durable inbox where needed
            opts.LocalQueue("payment-initiation")
                .UseDurableInbox(); // Important operations should use durable inbox
            
            opts.LocalQueue("payment-reservation")
                .UseDurableInbox(); // Financial operations need durability
            
            opts.LocalQueue("payment-settlement")
                .UseDurableInbox(); // Critical for settlement operations
            
            opts.LocalQueue("payment-cancellation");
            
            opts.LocalQueue("payment-workflow")
                .UseDurableInbox(); // Workflow continuations need durability
            
            // Configure dead letter queues
            opts.LocalQueue("payment-initiation-dlq");
            opts.LocalQueue("payment-reservation-dlq");
            opts.LocalQueue("payment-settlement-dlq");
            opts.LocalQueue("payment-cancellation-dlq");
            opts.LocalQueue("payment-workflow-dlq");
            
            // Configure retry and error handling policies
            opts.Policies.OnException<TimeoutException>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250));
            opts.Policies.OnException<HttpRequestException>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));
            
            // Enable durable outbox for all sending endpoints (requires Marten integration)
            //opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
            
            // Automatically apply transactions (works with Marten transactional middleware)
            opts.Policies.AutoApplyTransactions();
            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware<ModuleAccessMiddleware>();

            opts.UseNewtonsoftForSerialization();
            opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
            
            opts.Discovery.CustomizeHandlerDiscovery(q =>
            {
                q.Includes.WithNameSuffix("UseCase");
            });
            
            opts.Services.AddResourceSetupOnStartup();
            opts.Services.AddSingleton<IModuleAccessPolicy, ModuleAccessPolicy>();
        });
    }
}