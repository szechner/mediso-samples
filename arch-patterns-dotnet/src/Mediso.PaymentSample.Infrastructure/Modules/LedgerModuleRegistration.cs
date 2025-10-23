using Mediso.PaymentSample.SharedKernel.Modules;
using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.Modules;

/// <summary>
/// Module registration for Ledger bounded context
/// </summary>
public sealed class LedgerModuleRegistration : IModuleRegistration
{
    public string ModuleName => "Ledger";
    
    public string[] Dependencies => Array.Empty<string>(); // Ledger is independent

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register ledger module facade using existing domain interface
        services.AddScoped<ILedgerModule>(provider => 
        {
            // This would normally be implemented by a concrete class that implements ILedgerModule
            // For now, return a placeholder implementation
            throw new NotImplementedException("Ledger module implementation not yet available");
        });
        
        // TODO: Register actual ledger services, repositories, and handlers when implemented
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<LedgerModuleRegistration>>();
        logger.LogInformation("Ledger module configured successfully");
    }

    public Task<ModuleHealthStatus> CheckHealthAsync(IServiceProvider serviceProvider)
    {
        try
        {
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