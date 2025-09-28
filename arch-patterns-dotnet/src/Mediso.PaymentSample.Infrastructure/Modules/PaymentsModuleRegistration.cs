using Mediso.PaymentSample.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.Modules;

/// <summary>
/// Module registration for Payments bounded context
/// </summary>
public sealed class PaymentsModuleRegistration : IModuleRegistration
{
    public string ModuleName => "Payments";
    
    public string[] Dependencies => new[] { "Accounts", "Ledger", "Compliance" };

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register payment module facade using existing domain interface
        services.AddScoped<IPaymentModuleFacade>(provider => 
        {
            // This would normally be implemented by a concrete class that implements IPaymentsModule
            // For now, return a placeholder implementation
            throw new NotImplementedException("Payment module implementation not yet available");
        });
        
        // Configure payment module settings if needed
        var paymentSettings = configuration.GetSection("PaymentModule");
        // services.Configure<PaymentModuleSettings>(paymentSettings);
        
        // TODO: Register actual payment services, repositories, and handlers when implemented
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        // Initialize payment module background services if needed
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PaymentsModuleRegistration>>();
        logger.LogInformation("Payments module configured successfully");
        
        // TODO: Initialize payment event subscriptions when implemented
    }

    public Task<ModuleHealthStatus> CheckHealthAsync(IServiceProvider serviceProvider)
    {
        try
        {
            // Basic health check - module is registered and responding
            var details = new Dictionary<string, object>
            {
                ["module_status"] = "registered",
                ["implementation_status"] = "placeholder",
                ["last_check"] = DateTimeOffset.UtcNow
            };

            return Task.FromResult(new ModuleHealthStatus(ModuleName, true, "Healthy", details));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ModuleHealthStatus(ModuleName, false, "Unhealthy", Exception: ex));
        }
    }
}