using System.Diagnostics;

namespace Mediso.PaymentSample.SharedKernel.Logging;

/// <summary>
/// Default implementation of ILoggingContext that integrates with Activity (distributed tracing)
/// </summary>
public class LoggingContext : ILoggingContext
{
    private readonly IDictionary<string, object> _properties;

    public LoggingContext() : this(Guid.NewGuid().ToString(), new Dictionary<string, object>())
    {
    }

    public LoggingContext(string correlationId) : this(correlationId, new Dictionary<string, object>())
    {
    }

    public LoggingContext(string correlationId, IDictionary<string, object> properties)
    {
        CorrelationId = correlationId;
        _properties = new Dictionary<string, object>(properties);
    }

    /// <inheritdoc />
    public string CorrelationId { get; }

    /// <inheritdoc />
    public string? TraceId => Activity.Current?.TraceId.ToString();

    /// <inheritdoc />
    public string? SpanId => Activity.Current?.SpanId.ToString();

    /// <inheritdoc />
    public IDictionary<string, object> Properties => new Dictionary<string, object>(_properties);

    /// <inheritdoc />
    public ILoggingContext WithProperties(IDictionary<string, object> properties)
    {
        var combined = new Dictionary<string, object>(_properties);
        foreach (var kvp in properties)
        {
            combined[kvp.Key] = kvp.Value;
        }
        return new LoggingContext(CorrelationId, combined);
    }

    /// <inheritdoc />
    public ILoggingContext WithProperty(string key, object value)
    {
        return WithProperties(new Dictionary<string, object> { [key] = value });
    }
}