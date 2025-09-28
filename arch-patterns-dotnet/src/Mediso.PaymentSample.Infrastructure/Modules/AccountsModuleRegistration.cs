using Mediso.PaymentSample.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.Modules;

/// <summary>
/// Module registration for Accounts bounded context
/// </summary>
public sealed class AccountsModuleRegistration : IModuleRegistration
{
    public string ModuleName => "Accounts";
    
    public string[] Dependencies => new[] { "Ledger" };

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register account module facade using existing domain interface
        services.AddScoped<IAccountModuleFacade>(provider => 
        {
            // This would normally be implemented by a concrete class that implements IAccountsModule
            // For now, return a placeholder implementation
            throw new NotImplementedException("Account module implementation not yet available");
        });
        
        // Configure account module settings if needed
        var accountSettings = configuration.GetSection("AccountModule");
        // services.Configure<AccountModuleSettings>(accountSettings);
        
        // TODO: Register actual account services, repositories, and handlers when implemented
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        // Initialize account module background services if needed
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AccountsModuleRegistration>>();
        logger.LogInformation("Accounts module configured successfully");
        
        // TODO: Initialize account event subscriptions when implemented
    }

    public async Task<ModuleHealthStatus> CheckHealthAsync(IServiceProvider serviceProvider)
    {
        try
        {
            // Basic health check - module is registered and responding
            var details = new Dictionary<string, object>
            {
                ["module_status"] = "registered",
                ["implementation_status"] = "placeholder", 
                ["last_check"] = DateTime.UtcNow
            };

            return new ModuleHealthStatus(ModuleName, true, "Healthy", details);
        }
        catch (Exception ex)
        {
            return new ModuleHealthStatus(ModuleName, false, "Unhealthy", Exception: ex);
        }
    }
}