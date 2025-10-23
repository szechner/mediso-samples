namespace Mediso.PaymentSample.SharedKernel.Logging;

/// <summary>
/// Provides a context for structured logging with correlation tracking
/// </summary>
public interface ILoggingContext
{
    /// <summary>
    /// Gets the correlation ID for the current operation
    /// </summary>
    string CorrelationId { get; }
    
    /// <summary>
    /// Gets the trace ID for the current operation
    /// </summary>
    string? TraceId { get; }
    
    /// <summary>
    /// Gets the span ID for the current operation
    /// </summary>
    string? SpanId { get; }
    
    /// <summary>
    /// Gets additional context properties for logging
    /// </summary>
    IDictionary<string, object> Properties { get; }
    
    /// <summary>
    /// Creates a scoped logging context with additional properties
    /// </summary>
    /// <param name="properties">Additional properties to include in the context</param>
    /// <returns>A new logging context with the combined properties</returns>
    ILoggingContext WithProperties(IDictionary<string, object> properties);
    
    /// <summary>
    /// Creates a scoped logging context with a single additional property
    /// </summary>
    /// <param name="key">The property key</param>
    /// <param name="value">The property value</param>
    /// <returns>A new logging context with the additional property</returns>
    ILoggingContext WithProperty(string key, object value);
}