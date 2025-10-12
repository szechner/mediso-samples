using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Threading.RateLimiting;

namespace Mediso.PaymentSample.Application.Common.Resilience;

/// <summary>
/// Provides configured resilience pipelines for different payment operations.
/// Implements circuit breaker, retry, timeout, and rate limiting patterns
/// with comprehensive observability and metrics.
/// </summary>
public interface IResiliencePipelineProvider
{
    /// <summary>
    /// Gets a configured resilience pipeline by name.
    /// </summary>
    /// <param name="pipelineName">Name of the pipeline (e.g., "payment-initiation")</param>
    /// <returns>Configured resilience pipeline</returns>
    ResiliencePipeline GetPipeline(string pipelineName);
    
    /// <summary>
    /// Gets a generic resilience pipeline with specified configuration.
    /// </summary>
    /// <typeparam name="T">Return type for the pipeline</typeparam>
    /// <param name="pipelineName">Name of the pipeline</param>
    /// <returns>Configured generic resilience pipeline</returns>
    ResiliencePipeline<T> GetPipeline<T>(string pipelineName);
}

/// <summary>
/// Implementation of resilience pipeline provider with pre-configured patterns
/// for payment processing operations.
/// </summary>
public sealed class PaymentResiliencePipelineProvider : IResiliencePipelineProvider, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Resilience");
    
    private readonly Dictionary<string, ResiliencePipeline> _pipelines;
    private readonly ILogger<PaymentResiliencePipelineProvider> _logger;
    private readonly ResilienceConfiguration _config;
    
    public PaymentResiliencePipelineProvider(
        IOptions<ResilienceConfiguration> config,
        ILogger<PaymentResiliencePipelineProvider> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _pipelines = new Dictionary<string, ResiliencePipeline>(StringComparer.OrdinalIgnoreCase);
        
        InitializePipelines();
    }

    public ResiliencePipeline GetPipeline(string pipelineName)
    {
        if (_pipelines.TryGetValue(pipelineName, out var pipeline))
        {
            return pipeline;
        }

        _logger.LogWarning("Resilience pipeline '{PipelineName}' not found, returning default pipeline", pipelineName);
        return _pipelines["default"];
    }

    public ResiliencePipeline<T> GetPipeline<T>(string pipelineName)
    {
        // For this implementation, we'll use the same pipeline logic
        // In a more advanced scenario, you might have type-specific pipelines
        var basePipeline = GetPipeline(pipelineName);
        return new ResiliencePipelineBuilder<T>()
            .AddPipeline(basePipeline)
            .Build();
    }

    private void InitializePipelines()
    {
        _pipelines["default"] = CreateDefaultPipeline();
        _pipelines["payment-initiation"] = CreatePaymentInitiationPipeline();
        _pipelines["payment-reservation"] = CreatePaymentReservationPipeline();
        _pipelines["payment-settlement"] = CreatePaymentSettlementPipeline();
        _pipelines["payment-cancellation"] = CreatePaymentCancellationPipeline();
        _pipelines["fraud-detection"] = CreateFraudDetectionPipeline();
        _pipelines["external-api"] = CreateExternalApiPipeline();
        _pipelines["database"] = CreateDatabasePipeline();
        _pipelines["event-store"] = CreateEventStorePipeline();
        
        _logger.LogInformation("Initialized {PipelineCount} resilience pipelines", _pipelines.Count);
    }

    private ResiliencePipeline CreateDefaultPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 3,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)TimeSpan.FromMilliseconds(100 * Math.Pow(2, args.AttemptNumber))),
                OnRetry = args =>
                {
                    using var activity = ActivitySource.StartActivity("Resilience.Retry");
                    activity?.SetTag("retry.attempt", args.AttemptNumber);
                    activity?.SetTag("retry.delay", args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _logger.LogWarning("Circuit breaker opened for default pipeline");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker closed for default pipeline");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    private ResiliencePipeline CreatePaymentInitiationPipeline()
    {
        return new ResiliencePipelineBuilder()
            // Retry strategy for payment initiation
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<OperationCanceledException>(),
                MaxRetryAttempts = _config.PaymentInitiation.MaxRetries,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)
                    TimeSpan.FromMilliseconds(500 * Math.Pow(1.5, args.AttemptNumber))),
                OnRetry = args =>
                {
                    _logger.LogDebug("Retrying payment initiation, attempt {AttemptNumber}", args.AttemptNumber);
                    using var activity = ActivitySource.StartActivity("Resilience.PaymentInitiation.Retry");
                    activity?.SetTag("retry.attempt", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            // Circuit breaker for payment initiation
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                FailureRatio = _config.PaymentInitiation.CircuitBreakerFailureRatio,
                MinimumThroughput = _config.PaymentInitiation.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_config.PaymentInitiation.CircuitBreakerSamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(_config.PaymentInitiation.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning("Payment initiation circuit breaker opened");
                    using var activity = ActivitySource.StartActivity("Resilience.PaymentInitiation.CircuitBreakerOpened");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Payment initiation circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            // Rate limiting for payment initiation
            .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = _config.PaymentInitiation.RateLimitPerMinute,
                SegmentsPerWindow = 6
            }))
            // Timeout for payment initiation
            .AddTimeout(TimeSpan.FromSeconds(_config.PaymentInitiation.TimeoutSeconds))
            .Build();
    }

    private ResiliencePipeline CreatePaymentReservationPipeline()
    {
        return new ResiliencePipelineBuilder()
            // Fewer retries for reservation due to time sensitivity
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<HttpRequestException>(ex => !IsNonRetryableHttpError(ex)),
                MaxRetryAttempts = _config.PaymentReservation.MaxRetries,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)
                    TimeSpan.FromMilliseconds(300 * Math.Pow(1.3, args.AttemptNumber))),
                OnRetry = args =>
                {
                    _logger.LogDebug("Retrying payment reservation, attempt {AttemptNumber}", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            // More aggressive circuit breaker for fraud detection timeouts
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<HttpRequestException>(),
                FailureRatio = _config.PaymentReservation.CircuitBreakerFailureRatio,
                MinimumThroughput = _config.PaymentReservation.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_config.PaymentReservation.CircuitBreakerSamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(_config.PaymentReservation.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning("Payment reservation circuit breaker opened");
                    return ValueTask.CompletedTask;
                }
            })
            // Bulkhead isolation with semaphore
            .AddRateLimiter(new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = _config.PaymentReservation.MaxConcurrency,
                QueueLimit = _config.PaymentReservation.MaxQueuedRequests
            }))
            // Shorter timeout for reservation
            .AddTimeout(TimeSpan.FromSeconds(_config.PaymentReservation.TimeoutSeconds))
            .Build();
    }

    private ResiliencePipeline CreatePaymentSettlementPipeline()
    {
        return new ResiliencePipelineBuilder()
            // More retries for settlement due to financial importance
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<OperationCanceledException>(),
                MaxRetryAttempts = _config.PaymentSettlement.MaxRetries,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)
                    TimeSpan.FromSeconds(1 + args.AttemptNumber * 2)), // Linear backoff for settlement
                OnRetry = args =>
                {
                    _logger.LogWarning("Retrying payment settlement, attempt {AttemptNumber}, this is critical!", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            // Conservative circuit breaker for settlement
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
                FailureRatio = _config.PaymentSettlement.CircuitBreakerFailureRatio,
                MinimumThroughput = _config.PaymentSettlement.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_config.PaymentSettlement.CircuitBreakerSamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(_config.PaymentSettlement.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogError("CRITICAL: Payment settlement circuit breaker opened!");
                    return ValueTask.CompletedTask;
                }
            })
            // Higher concurrency limit for settlement
            .AddRateLimiter(new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = _config.PaymentSettlement.MaxConcurrency,
                QueueLimit = _config.PaymentSettlement.MaxQueuedRequests
            }))
            // Longer timeout for settlement operations
            .AddTimeout(TimeSpan.FromSeconds(_config.PaymentSettlement.TimeoutSeconds))
            .Build();
    }

    private ResiliencePipeline CreatePaymentCancellationPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = _config.PaymentCancellation.MaxRetries,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)
                    TimeSpan.FromMilliseconds(400 * Math.Pow(1.4, args.AttemptNumber))),
                OnRetry = args =>
                {
                    _logger.LogDebug("Retrying payment cancellation, attempt {AttemptNumber}", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
                FailureRatio = _config.PaymentCancellation.CircuitBreakerFailureRatio,
                MinimumThroughput = _config.PaymentCancellation.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_config.PaymentCancellation.CircuitBreakerSamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(_config.PaymentCancellation.CircuitBreakerBreakDurationSeconds)
            })
            .AddTimeout(TimeSpan.FromSeconds(_config.PaymentCancellation.TimeoutSeconds))
            .Build();
    }

    private ResiliencePipeline CreateFraudDetectionPipeline()
    {
        return new ResiliencePipelineBuilder()
            // Fast fail for fraud detection to avoid blocking payments
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<HttpRequestException>(ex => IsRetryableHttpError(ex)),
                MaxRetryAttempts = 1, // Only one retry for fraud detection
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)TimeSpan.FromMilliseconds(100))
            })
            // Very aggressive circuit breaker
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 0.3, // Lower threshold
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(20),
                BreakDuration = TimeSpan.FromSeconds(15), // Shorter break duration
                OnOpened = args =>
                {
                    _logger.LogWarning("Fraud detection circuit breaker opened - fraud checks will be bypassed");
                    return ValueTask.CompletedTask;
                }
            })
            // Short timeout for fraud detection
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();
    }

    private ResiliencePipeline CreateExternalApiPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = 3,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)
                    TimeSpan.FromMilliseconds(200 * Math.Pow(2, args.AttemptNumber)))
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
                FailureRatio = 0.6,
                MinimumThroughput = 8,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(45)
            })
            .AddRateLimiter(new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                QueueLimit = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokensPerPeriod = 10
            }))
            .AddTimeout(TimeSpan.FromSeconds(15))
            .Build();
    }

    private ResiliencePipeline CreateDatabasePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<OperationCanceledException>()
                    .Handle<InvalidOperationException>(ex => ex.Message.Contains("connection")),
                MaxRetryAttempts = 2,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)
                    TimeSpan.FromMilliseconds(50 * args.AttemptNumber))
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<InvalidOperationException>(),
                FailureRatio = 0.7,
                MinimumThroughput = 15,
                SamplingDuration = TimeSpan.FromSeconds(45),
                BreakDuration = TimeSpan.FromSeconds(20)
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }

    private ResiliencePipeline CreateEventStorePipeline()
    {
        return new ResiliencePipelineBuilder()
            // Retry strategy for event store operations
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<OperationCanceledException>()
                    .Handle<InvalidOperationException>(ex => ex.Message.Contains("connection") || ex.Message.Contains("stream")),
                MaxRetryAttempts = 3,
                DelayGenerator = static args => ValueTask.FromResult((TimeSpan?)
                    TimeSpan.FromMilliseconds(100 * Math.Pow(1.5, args.AttemptNumber))),
                OnRetry = args =>
                {
                    _logger.LogDebug("Retrying event store operation, attempt {AttemptNumber}", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            // Circuit breaker for event store operations
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<InvalidOperationException>(),
                FailureRatio = 0.6,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                OnOpened = args =>
                {
                    _logger.LogWarning("Event store circuit breaker opened");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Event store circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            // Bulkhead isolation for event store
            .AddRateLimiter(new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = 25,
                QueueLimit = 100
            }))
            // Timeout for event store operations
            .AddTimeout(TimeSpan.FromSeconds(15))
            .Build();
    }

    private static bool IsRetryableHttpError(HttpRequestException ex)
    {
        // Retry on server errors (5xx) but not client errors (4xx)
        return ex.Message.Contains("5") || ex.Message.Contains("timeout");
    }

    private static bool IsNonRetryableHttpError(HttpRequestException ex)
    {
        // Don't retry on authentication errors, bad requests, etc.
        return ex.Message.Contains("401") || ex.Message.Contains("400") || ex.Message.Contains("403");
    }

    public void Dispose()
    {
        foreach (var pipeline in _pipelines.Values)
        {
            // ResiliencePipeline in Polly v8 doesn't implement IDisposable
            // Disposal is handled automatically by the framework
        }
        _pipelines.Clear();
        ActivitySource.Dispose();
    }
}

/// <summary>
/// Configuration for resilience patterns in payment processing.
/// </summary>
public sealed class ResilienceConfiguration
{
    public PaymentInitiationConfig PaymentInitiation { get; set; } = new();
    public PaymentReservationConfig PaymentReservation { get; set; } = new();
    public PaymentSettlementConfig PaymentSettlement { get; set; } = new();
    public PaymentCancellationConfig PaymentCancellation { get; set; } = new();
}

public sealed class PaymentInitiationConfig
{
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;
    public int RateLimitPerMinute { get; set; } = 100;
}

public sealed class PaymentReservationConfig
{
    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 20;
    public double CircuitBreakerFailureRatio { get; set; } = 0.4;
    public int CircuitBreakerMinimumThroughput { get; set; } = 8;
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 25;
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 20;
    public int MaxConcurrency { get; set; } = 20;
    public int MaxQueuedRequests { get; set; } = 50;
}

public sealed class PaymentSettlementConfig
{
    public int MaxRetries { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 60;
    public double CircuitBreakerFailureRatio { get; set; } = 0.6;
    public int CircuitBreakerMinimumThroughput { get; set; } = 12;
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 40;
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 45;
    public int MaxConcurrency { get; set; } = 30;
    public int MaxQueuedRequests { get; set; } = 100;
}

public sealed class PaymentCancellationConfig
{
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 25;
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; set; } = 8;
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 25;
}