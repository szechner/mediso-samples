using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.SharedKernel.Modules;

/// <summary>
/// Default implementation of module access policy
/// </summary>
public sealed class ModuleAccessPolicy : IModuleAccessPolicy
{
    private readonly Dictionary<string, HashSet<string>> _modulePermissions = new()
    {
        // Payments module can access these modules
        ["Payments"] = new HashSet<string> { "Accounts", "Ledger", "Compliance", "Notifications" },
        
        // Accounts module has restricted access
        ["Accounts"] = new HashSet<string> { "Ledger" },
        
        // Ledger module is mostly independent
        ["Ledger"] = new HashSet<string>(),
        
        // Compliance module can access accounts for risk assessment
        ["Compliance"] = new HashSet<string> { "Accounts" },
        
        // Notifications module is read-only
        ["Notifications"] = new HashSet<string>()
    };

    private readonly Dictionary<(string, string), HashSet<string>> _operationPermissions = new()
    {
        // Payments → Accounts operations
        [("Payments", "Accounts")] = new HashSet<string> { "GetBalance", "Reserve", "Release", "HasSufficientFunds" },
        
        // Payments → Compliance operations
        [("Payments", "Compliance")] = new HashSet<string> { "ScreenPayment", "IsWithinLimits" },
        
        // Payments → Ledger operations
        [("Payments", "Ledger")] = new HashSet<string> { "CreateJournalEntries", "GetEntries" },
        
        // Compliance → Accounts operations (read-only)
        [("Compliance", "Accounts")] = new HashSet<string> { "GetBalance", "GetProfile" },
        
        // Accounts → Ledger operations
        [("Accounts", "Ledger")] = new HashSet<string> { "ValidateBalance", "GetLedgerBalance" }
    };

    public bool CanAccess(string fromModule, string toModule, string operation)
    {
        // Module can always access itself
        if (fromModule == toModule)
            return true;

        // Check if module is allowed to access target module
        if (!_modulePermissions.TryGetValue(fromModule, out var allowedModules) || 
            !allowedModules.Contains(toModule))
        {
            return false;
        }

        // Check specific operation permissions
        var key = (fromModule, toModule);
        if (!_operationPermissions.TryGetValue(key, out var allowedOperations))
        {
            // If no specific operations defined, allow all operations for permitted modules
            return true;
        }

        return allowedOperations.Contains(operation);
    }

    public void ValidateAccess(string fromModule, string toModule, string operation)
    {
        if (!CanAccess(fromModule, toModule, operation))
        {
            throw new ModuleAccessException(
                $"Module '{fromModule}' is not allowed to perform operation '{operation}' on module '{toModule}'");
        }
    }
}

/// <summary>
/// Exception thrown when module access is denied
/// </summary>
public sealed class ModuleAccessException : DomainException
{
    public ModuleAccessException(string message) : base(message) { }
    public ModuleAccessException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Attribute to mark module boundaries and enforce access control
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ModuleBoundaryAttribute : Attribute
{
    public string ModuleName { get; }
    public string[] AllowedCallers { get; }

    public ModuleBoundaryAttribute(string moduleName, params string[] allowedCallers)
    {
        ModuleName = moduleName;
        AllowedCallers = allowedCallers;
    }
}

/// <summary>
/// Interceptor for enforcing module access policies
/// </summary>
public sealed class ModuleAccessInterceptor
{
    private readonly IModuleAccessPolicy _policy;

    public ModuleAccessInterceptor(IModuleAccessPolicy policy)
    {
        _policy = policy;
    }

    public void ValidateAccess(string callingModule, string targetModule, string operation)
    {
        _policy.ValidateAccess(callingModule, targetModule, operation);
    }
}