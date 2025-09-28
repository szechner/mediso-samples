using Mediso.PaymentSample.SharedKernel.Modules;
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
        services.AddScoped<IComplianceModuleFacade>(provider => 
        {
            // This would normally be implemented by a concrete class that implements IComplianceModule
            // For now, return a placeholder implementation
            throw new NotImplementedException("Compliance module implementation not yet available");
        });
        
        // TODO: Register actual compliance services, repositories, and handlers when implemented
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ComplianceModuleRegistration>>();
        logger.LogInformation("Compliance module configured successfully");
    }

    public async Task<ModuleHealthStatus> CheckHealthAsync(IServiceProvider serviceProvider)
    {
        try
        {
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