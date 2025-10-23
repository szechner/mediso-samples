using Mediso.PaymentSample.SharedKernel.Modules;
using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.Modules;

/// <summary>
/// Module registration for Compliance bounded context
/// </summary>
public sealed class ComplianceModuleRegistration : IModuleRegistration
{
    public string ModuleName => "Compliance";
    
    public string[] Dependencies => new[] { "Accounts" };

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register compliance module facade using existing domain interface
        services.AddScoped<IComplianceModule>(provider => 
        {
            // This would normally be implemented by a concrete class that implements IComplianceModule
            // For now, return a placeholder implementation
            throw new NotImplementedException("Compliance module implementation not yet available");
        });
        
        // TODO: Register actual compliance services, repositories, and handlers when implemented
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ComplianceModuleRegistration>>();
        logger.LogInformation("Compliance module configured successfully");
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