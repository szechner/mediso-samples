using Mediso.PaymentSample.Infrastructure.Modules;
using Mediso.PaymentSample.SharedKernel.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mediso.PaymentSample.Examples;

/// <summary>
/// Example showing how to bootstrap the modular monolith architecture
/// </summary>
public class ModularBootstrapExample
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure services with modular architecture
        ConfigureServices(builder.Services, builder.Configuration);

        var app = builder.Build();

        // Configure the HTTP request pipeline
        ConfigureMiddleware(app);

        // Configure modules after DI container is built
        app.Services.ConfigureModules();

        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add basic services
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Add logging
        services.AddLogging();

        // Add health checks
        services.AddHealthChecks();

        // Configure modular architecture
        services.AddModularArchitecture(configuration, bootstrapper =>
        {
            // Register modules in dependency order
            // Note: The bootstrapper will automatically sort by dependencies
            
            bootstrapper
                .RegisterModule<LedgerModuleRegistration>()           // No dependencies
                .RegisterModule<AccountsModuleRegistration>()        // Depends on Ledger
                .RegisterModule<ComplianceModuleRegistration>()      // Depends on Accounts
                .RegisterModule<PaymentsModuleRegistration>();       // Depends on Accounts, Ledger, Compliance
        });

        // Add any cross-cutting concerns
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ModularBootstrapExample>());
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        // Add custom health check endpoint that includes module health
        app.MapGet("/health/modules", async (IServiceProvider serviceProvider) =>
        {
            var bootstrapper = serviceProvider.GetRequiredService<ModuleBootstrapper>();
            var health = await bootstrapper.CheckHealthAsync(serviceProvider);
            
            return Results.Ok(new
            {
                timestamp = DateTime.UtcNow,
                overall_status = health.All(h => h.Value.IsHealthy) ? "Healthy" : "Unhealthy",
                modules = health
            });
        });
    }
}

/// <summary>
/// Example controller showing how to use module facades
/// </summary>
[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
public class PaymentController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly IPaymentModuleFacade _paymentFacade;
    private readonly IAccountModuleFacade _accountFacade;
    private readonly IComplianceModuleFacade _complianceFacade;
    private readonly ModuleAccessInterceptor _accessInterceptor;

    public PaymentController(
        IPaymentModuleFacade paymentFacade,
        IAccountModuleFacade accountFacade, 
        IComplianceModuleFacade complianceFacade,
        ModuleAccessInterceptor accessInterceptor)
    {
        _paymentFacade = paymentFacade;
        _accountFacade = accountFacade;
        _complianceFacade = complianceFacade;
        _accessInterceptor = accessInterceptor;
    }

    [Microsoft.AspNetCore.Mvc.HttpPost]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> CreatePayment(
        [Microsoft.AspNetCore.Mvc.FromBody] CreatePaymentRequest request)
    {
        try
        {
            // Validate cross-module access (optional - for demonstration)
            _accessInterceptor.ValidateAccess("API", "Payments", "CreatePayment");

            // Create payment using the facade
            var paymentId = await _paymentFacade.CreatePaymentAsync(request);

            return Ok(new { paymentId = paymentId, reference = request.Reference });
        }
        catch (ModuleAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    [Microsoft.AspNetCore.Mvc.HttpGet("{accountId}/balance")]
    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> GetAccountBalance(Guid accountId)
    {
        try
        {
            // Validate cross-module access
            _accessInterceptor.ValidateAccess("API", "Accounts", "GetBalance");

            // Get account balance using the facade
            var balance = await _accountFacade.GetBalanceAsync(accountId);

            return Ok(balance);
        }
        catch (ModuleAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }
}

/// <summary>
/// Example configuration for appsettings.json
/// </summary>
public static class ConfigurationExample
{
    public const string AppSettingsExample = @"
{
  ""PaymentModule"": {
    ""MaxPaymentAmount"": 1000000,
    ""DefaultCurrency"": ""USD"",
    ""EnableComplianceScreening"": true
  },
  ""AccountModule"": {
    ""DefaultReservationExpirationMinutes"": 30,
    ""EnableBalanceValidation"": true
  },
  ""ComplianceModule"": {
    ""AMLServiceEndpoint"": ""https://aml-service.example.com"",
    ""RiskThreshold"": 75,
    ""EnableRealTimeScreening"": true
  },
  ""LedgerModule"": {
    ""EnableBalanceReconciliation"": true,
    ""ReconciliationIntervalMinutes"": 60
  }
}";
}