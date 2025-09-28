using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.SharedKernel.Modules;

/// <summary>
/// Base interface for module registration and configuration
/// </summary>
public interface IModuleRegistration
{
    /// <summary>
    /// Module name for identification
    /// </summary>
    string ModuleName { get; }
    
    /// <summary>
    /// Modules this module depends on
    /// </summary>
    string[] Dependencies { get; }
    
    /// <summary>
    /// Register module services
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
    
    /// <summary>
    /// Configure module after all services are registered
    /// </summary>
    void Configure(IServiceProvider serviceProvider);
    
    /// <summary>
    /// Health check for the module
    /// </summary>
    Task<ModuleHealthStatus> CheckHealthAsync(IServiceProvider serviceProvider);
}

/// <summary>
/// Health status for modules
/// </summary>
public sealed record ModuleHealthStatus(
    string ModuleName,
    bool IsHealthy,
    string Status,
    Dictionary<string, object>? Details = null,
    Exception? Exception = null);

/// <summary>
/// Module bootstrap and registration manager
/// </summary>
public sealed class ModuleBootstrapper
{
    private readonly List<IModuleRegistration> _modules = new();
    private readonly ILogger<ModuleBootstrapper> _logger;
    private readonly Dictionary<string, ModuleHealthStatus> _healthStatuses = new();

    public ModuleBootstrapper(ILogger<ModuleBootstrapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a module
    /// </summary>
    public ModuleBootstrapper RegisterModule<T>() where T : IModuleRegistration, new()
    {
        var module = new T();
        _modules.Add(module);
        _logger.LogInformation("Registered module: {ModuleName}", module.ModuleName);
        return this;
    }

    /// <summary>
    /// Register a module instance
    /// </summary>
    public ModuleBootstrapper RegisterModule(IModuleRegistration module)
    {
        _modules.Add(module);
        _logger.LogInformation("Registered module: {ModuleName}", module.ModuleName);
        return this;
    }

    /// <summary>
    /// Bootstrap all modules in dependency order
    /// </summary>
    public void Bootstrap(IServiceCollection services, IConfiguration configuration)
    {
        _logger.LogInformation("Starting module bootstrapping for {ModuleCount} modules", _modules.Count);

        // Validate dependencies
        ValidateDependencies();

        // Sort modules by dependencies
        var sortedModules = SortModulesByDependencies();

        // Register services for each module
        foreach (var module in sortedModules)
        {
            try
            {
                _logger.LogInformation("Registering services for module: {ModuleName}", module.ModuleName);
                module.RegisterServices(services, configuration);
                _logger.LogInformation("Successfully registered services for module: {ModuleName}", module.ModuleName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register services for module: {ModuleName}", module.ModuleName);
                throw new ModuleRegistrationException($"Failed to register module '{module.ModuleName}'", ex);
            }
        }

        // Register module access policy
        services.AddSingleton<IModuleAccessPolicy, ModuleAccessPolicy>();
        services.AddScoped<ModuleAccessInterceptor>();
        
        // Register module bootstrapper itself for later configuration
        services.AddSingleton(this);
        
        _logger.LogInformation("Module bootstrapping completed successfully");
    }

    /// <summary>
    /// Configure all modules after DI container is built
    /// </summary>
    public void Configure(IServiceProvider serviceProvider)
    {
        _logger.LogInformation("Starting module configuration");

        var sortedModules = SortModulesByDependencies();
        
        foreach (var module in sortedModules)
        {
            try
            {
                _logger.LogInformation("Configuring module: {ModuleName}", module.ModuleName);
                module.Configure(serviceProvider);
                _logger.LogInformation("Successfully configured module: {ModuleName}", module.ModuleName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure module: {ModuleName}", module.ModuleName);
                throw new ModuleConfigurationException($"Failed to configure module '{module.ModuleName}'", ex);
            }
        }

        _logger.LogInformation("Module configuration completed successfully");
    }

    /// <summary>
    /// Check health of all modules
    /// </summary>
    public async Task<Dictionary<string, ModuleHealthStatus>> CheckHealthAsync(IServiceProvider serviceProvider)
    {
        _logger.LogInformation("Starting health check for all modules");

        var healthTasks = _modules.Select(async module =>
        {
            try
            {
                var health = await module.CheckHealthAsync(serviceProvider);
                _healthStatuses[module.ModuleName] = health;
                return health;
            }
            catch (Exception ex)
            {
                var failedHealth = new ModuleHealthStatus(module.ModuleName, false, "Failed", Exception: ex);
                _healthStatuses[module.ModuleName] = failedHealth;
                _logger.LogError(ex, "Health check failed for module: {ModuleName}", module.ModuleName);
                return failedHealth;
            }
        });

        await Task.WhenAll(healthTasks);

        _logger.LogInformation("Health check completed. Healthy modules: {HealthyCount}, Unhealthy: {UnhealthyCount}",
            _healthStatuses.Values.Count(h => h.IsHealthy),
            _healthStatuses.Values.Count(h => !h.IsHealthy));

        return new Dictionary<string, ModuleHealthStatus>(_healthStatuses);
    }

    /// <summary>
    /// Get module health status
    /// </summary>
    public ModuleHealthStatus? GetModuleHealth(string moduleName)
    {
        return _healthStatuses.TryGetValue(moduleName, out var health) ? health : null;
    }

    private void ValidateDependencies()
    {
        var moduleNames = _modules.Select(m => m.ModuleName).ToHashSet();
        
        foreach (var module in _modules)
        {
            foreach (var dependency in module.Dependencies)
            {
                if (!moduleNames.Contains(dependency))
                {
                    throw new ModuleDependencyException(
                        $"Module '{module.ModuleName}' depends on '{dependency}' which is not registered");
                }
            }
        }
    }

    private List<IModuleRegistration> SortModulesByDependencies()
    {
        var sorted = new List<IModuleRegistration>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(IModuleRegistration module)
        {
            if (visiting.Contains(module.ModuleName))
            {
                throw new ModuleDependencyException(
                    $"Circular dependency detected involving module '{module.ModuleName}'");
            }
            
            if (visited.Contains(module.ModuleName))
                return;

            visiting.Add(module.ModuleName);

            foreach (var dependencyName in module.Dependencies)
            {
                var dependency = _modules.FirstOrDefault(m => m.ModuleName == dependencyName);
                if (dependency != null)
                {
                    Visit(dependency);
                }
            }

            visiting.Remove(module.ModuleName);
            visited.Add(module.ModuleName);
            sorted.Add(module);
        }

        foreach (var module in _modules)
        {
            Visit(module);
        }

        return sorted;
    }
}

/// <summary>
/// Exception thrown during module registration
/// </summary>
public sealed class ModuleRegistrationException : Exception
{
    public ModuleRegistrationException(string message) : base(message) { }
    public ModuleRegistrationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown during module configuration
/// </summary>
public sealed class ModuleConfigurationException : Exception
{
    public ModuleConfigurationException(string message) : base(message) { }
    public ModuleConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown for module dependency issues
/// </summary>
public sealed class ModuleDependencyException : Exception
{
    public ModuleDependencyException(string message) : base(message) { }
    public ModuleDependencyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Extension methods for easier module registration
/// </summary>
public static class ModuleRegistrationExtensions
{
    /// <summary>
    /// Add modular architecture support to services
    /// </summary>
    public static IServiceCollection AddModularArchitecture(
        this IServiceCollection services, 
        IConfiguration configuration,
        Action<ModuleBootstrapper> configure)
    {
        var logger = services.BuildServiceProvider().GetService<ILogger<ModuleBootstrapper>>() 
                     ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ModuleBootstrapper>.Instance;
        
        var bootstrapper = new ModuleBootstrapper(logger);
        configure(bootstrapper);
        bootstrapper.Bootstrap(services, configuration);

        return services;
    }

    /// <summary>
    /// Configure modules after DI container is built
    /// </summary>
    public static IServiceProvider ConfigureModules(this IServiceProvider serviceProvider)
    {
        var bootstrapper = serviceProvider.GetRequiredService<ModuleBootstrapper>();
        bootstrapper.Configure(serviceProvider);
        return serviceProvider;
    }
}