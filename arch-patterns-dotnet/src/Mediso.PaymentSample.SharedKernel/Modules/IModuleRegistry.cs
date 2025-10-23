using Microsoft.Extensions.DependencyInjection;

namespace Mediso.PaymentSample.SharedKernel.Modules;

/// <summary>
/// Interface for module registration in modular monolith
/// </summary>
public interface IModule
{
    string Name { get; }
    void RegisterServices(IServiceCollection services);
}

/// <summary>
/// Registry for managing modules in modular monolith
/// </summary>
public interface IModuleRegistry
{
    void RegisterModule<T>() where T : IModule, new();
    void RegisterModule(IModule module);
    IEnumerable<IModule> GetModules();
    T? GetModule<T>() where T : class, IModule;
}

/// <summary>
/// Module access policy for controlling cross-module boundaries
/// </summary>
public interface IModuleAccessPolicy
{
    bool CanAccess(string fromModule, string toModule, string operation);
    void ValidateAccess(string fromModule, string toModule, string operation);
}