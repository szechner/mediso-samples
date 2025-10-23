using Microsoft.Extensions.Logging;
using Mediso.PaymentSample.SharedKernel.Domain;
using System.Diagnostics;

namespace Mediso.PaymentSample.SharedKernel.Logging;

/// <summary>
/// Extension methods for structured logging with domain-specific context
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs with structured data from logging context
    /// </summary>
    public static void LogWithContext<T>(this ILogger<T> logger, LogLevel logLevel, string messageTemplate, ILoggingContext context, params object[] args)
    {
        if (!logger.IsEnabled(logLevel)) return;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.CorrelationId,
            ["TraceId"] = context.TraceId ?? "unknown",
            ["SpanId"] = context.SpanId ?? "unknown"
        });

        foreach (var property in context.Properties)
        {
            using var propertyScope = logger.BeginScope(new Dictionary<string, object> { [property.Key] = property.Value });
        }

        logger.Log(logLevel, messageTemplate, args);
    }

    /// <summary>
    /// Logs domain event with structured context
    /// </summary>
    public static void LogDomainEvent<T>(this ILogger<T> logger, IDomainEvent domainEvent, string message = "Domain event occurred")
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["EventType"] = domainEvent.GetType().Name,
            ["EventTimestamp"] = domainEvent.CreatedAt,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? "unknown",
            ["SpanId"] = Activity.Current?.SpanId.ToString() ?? "unknown"
        });

        logger.LogInformation("{Message}: {EventType} at {Timestamp}", 
            message, domainEvent.GetType().Name, domainEvent.CreatedAt);
    }

    /// <summary>
    /// Logs payment operation with structured context
    /// </summary>
    public static void LogPaymentOperation<T>(this ILogger<T> logger, string paymentId, string operation, LogLevel logLevel = LogLevel.Information, string? additionalInfo = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["PaymentId"] = paymentId,
            ["Operation"] = operation,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? "unknown",
            ["SpanId"] = Activity.Current?.SpanId.ToString() ?? "unknown"
        });

        var message = additionalInfo != null 
            ? "Payment {PaymentId} operation {Operation}: {AdditionalInfo}"
            : "Payment {PaymentId} operation {Operation}";

        var args = additionalInfo != null 
            ? new object[] { paymentId, operation, additionalInfo }
            : new object[] { paymentId, operation };

        logger.Log(logLevel, message, args);
    }

    /// <summary>
    /// Logs exception with structured context
    /// </summary>
    public static void LogExceptionWithContext<T>(this ILogger<T> logger, Exception exception, ILoggingContext context, string messageTemplate, params object[] args)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.CorrelationId,
            ["TraceId"] = context.TraceId ?? "unknown",
            ["SpanId"] = context.SpanId ?? "unknown",
            ["ExceptionType"] = exception.GetType().Name
        });

        foreach (var property in context.Properties)
        {
            using var propertyScope = logger.BeginScope(new Dictionary<string, object> { [property.Key] = property.Value });
        }

        logger.LogError(exception, messageTemplate, args);
    }

    /// <summary>
    /// Logs performance timing with structured context
    /// </summary>
    public static IDisposable LogTiming<T>(this ILogger<T> logger, string operationName, string? correlationId = null)
    {
        return new TimingLogger<T>(logger, operationName, correlationId);
    }

    private class TimingLogger<T> : IDisposable
    {
        private readonly ILogger<T> _logger;
        private readonly string _operationName;
        private readonly string _correlationId;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public TimingLogger(ILogger<T> logger, string operationName, string? correlationId)
        {
            _logger = logger;
            _operationName = operationName;
            _correlationId = correlationId ?? Guid.NewGuid().ToString();
            _stopwatch = Stopwatch.StartNew();

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["OperationName"] = _operationName,
                ["CorrelationId"] = _correlationId,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? "unknown"
            });

            _logger.LogDebug("Starting operation {OperationName}", _operationName);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _stopwatch.Stop();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["OperationName"] = _operationName,
                ["CorrelationId"] = _correlationId,
                ["Duration"] = _stopwatch.ElapsedMilliseconds,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? "unknown"
            });

            _logger.LogInformation("Completed operation {OperationName} in {Duration}ms", 
                _operationName, _stopwatch.ElapsedMilliseconds);

            _disposed = true;
        }
    }
}