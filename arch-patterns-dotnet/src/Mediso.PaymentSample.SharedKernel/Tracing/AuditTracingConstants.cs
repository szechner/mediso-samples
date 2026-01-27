using System.Diagnostics;

namespace Mediso.PaymentSample.SharedKernel.Tracing;

/// <summary>
/// Constants for OpenTelemetry tracing and activity sources
/// </summary>
public static class AuditTracingConstants
{
    /// <summary>
    /// The service name used for tracing
    /// </summary>
    public const string ServiceName = "Mediso.AuditSample";

    /// <summary>
    /// The service version for tracing
    /// </summary>
    public const string ServiceVersion = "1.0.0";
    
    /// <summary>
    /// Service names for different layers
    /// </summary>
    public const string DomainServiceName = "Mediso.AuditSample.Domain";
    public const string ApplicationServiceName = "Mediso.AuditSample.Application";
    public const string ApiServiceName = "Mediso.AuditSample.Api";
    public const string InfrastructureServiceName = "Mediso.AuditSample.Infrastructure";

    /// <summary>
    /// Activity source for domain operations
    /// </summary>
    public static readonly ActivitySource DomainActivitySource = new(
        $"{ServiceName}.Domain", 
        ServiceVersion);

    /// <summary>
    /// Activity source for application operations
    /// </summary>
    public static readonly ActivitySource ApplicationActivitySource = new(
        $"{ServiceName}.Application", 
        ServiceVersion);

    /// <summary>
    /// Activity source for API operations
    /// </summary>
    public static readonly ActivitySource ApiActivitySource = new(
        $"{ServiceName}.Api", 
        ServiceVersion);

    /// <summary>
    /// Activity source for infrastructure operations
    /// </summary>
    public static readonly ActivitySource InfrastructureActivitySource = new(
        $"{ServiceName}.Infrastructure", 
        ServiceVersion);

    // Common tag constants for backward compatibility
    public const string CorrelationId = "correlation.id";
    
    // Tag names for consistent tagging across the application
    public static class Tags
    {
        public const string CorrelationId = "correlation.id";
        public const string DatabaseOperation = "database.operation";
    }

    // Activity names for common operations
    public static class Activities
    {
        // Domain Activities

        
        // Application Activities

        
        // API Activities
        public const string HttpRequest = "api.http-request";
        public const string Validation = "api.validation";
        
        // Infrastructure Activities

    }
}