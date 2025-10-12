using System.Diagnostics;
using Mediso.PaymentSample.Application.Common.Resilience;
using Mediso.PaymentSample.Application.Modules.FraudDetection.Contracts;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Mediso.PaymentSample.Application.Modules.FraudDetection.Handlers;

/// <summary>
/// Wolverine handlers for fraud detection commands and events.
/// 
/// This module provides:
/// - Real-time fraud detection analysis
/// - Integration with external fraud detection providers
/// - Risk scoring and decision making
/// - Comprehensive audit trails and observability
/// - Fallback mechanisms for provider failures
/// 
/// Architecture patterns:
/// - Command Handler Pattern for processing fraud detection requests
/// - Event-Driven Architecture for asynchronous processing
/// - Circuit Breaker Pattern for external service resilience
/// - Bulkhead Pattern for isolation of different fraud providers
/// - Comprehensive observability with distributed tracing
/// </summary>
public static class FraudDetectionHandler
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.FraudDetection");

    /// <summary>
    /// Handles fraud detection command with comprehensive analysis pipeline.
    /// 
    /// Processing pipeline:
    /// 1. Input validation and sanitization
    /// 2. Customer history analysis
    /// 3. Behavioral pattern analysis
    /// 4. Velocity checks
    /// 5. Geographic risk assessment
    /// 6. External provider integration
    /// 7. Rule engine evaluation
    /// 8. Final risk scoring and decision
    /// </summary>
    public static async Task Handle(
        PerformFraudDetectionCommand command,
        IMessageBus messageBus,
        IFraudDetectionService fraudDetectionService,
        IResiliencePipelineProvider resilienceProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("FraudDetection.PerformAnalysis");
        activity?.SetTag(TracingConstants.CorrelationId, command.CorrelationId);
        activity?.SetTag("payment.id", command.PaymentId.Value);
        activity?.SetTag("customer.id", command.CustomerId.Value);
        activity?.SetTag("payment.amount", command.Amount);
        activity?.SetTag("payment.currency", command.Currency);
        activity?.SetTag("saga.id", command.PaymentProcessingSagaId);

        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "Starting fraud detection analysis for payment {PaymentId}, amount {Amount} {Currency} [CorrelationId: {CorrelationId}, SagaId: {SagaId}]",
            command.PaymentId, command.Amount, command.Currency, command.CorrelationId, command.PaymentProcessingSagaId);

        try
        {
            // Get resilience pipeline for fraud detection services
            var pipeline = resilienceProvider.GetPipeline("fraud-detection");
            
            // Perform fraud detection analysis with resilience
            var analysisResult = await pipeline.ExecuteAsync(async ct =>
            {
                using var analysisActivity = ActivitySource.StartActivity("FraudDetection.Analysis");
                return await fraudDetectionService.AnalyzePaymentAsync(command, ct);
            }, cancellationToken);

            // Create and publish completion event
            var completionEvent = new FraudDetectionCompletedEvent
            {
                PaymentId = command.PaymentId,
                RiskLevel = analysisResult.RiskLevel,
                RiskScore = analysisResult.RiskScore,
                Provider = analysisResult.Provider,
                AnalyzedAt = DateTimeOffset.UtcNow,
                RiskFactors = analysisResult.RiskFactors,
                Recommendations = analysisResult.Recommendations,
                AnalysisDetails = analysisResult.AnalysisDetails,
                PaymentProcessingSagaId = command.PaymentProcessingSagaId,
                ProcessingDuration = stopwatch.Elapsed
            };

            // Send event back to saga for orchestration
            await messageBus.PublishAsync(completionEvent);

            logger.LogInformation(
                "Fraud detection completed for payment {PaymentId} - Risk Level: {RiskLevel}, Score: {RiskScore}, Duration: {Duration}ms [CorrelationId: {CorrelationId}]",
                command.PaymentId, analysisResult.RiskLevel, analysisResult.RiskScore, 
                stopwatch.ElapsedMilliseconds, command.CorrelationId);

            activity?.SetTag("fraud.risk_level", analysisResult.RiskLevel.ToString());
            activity?.SetTag("fraud.risk_score", analysisResult.RiskScore);
            activity?.SetTag("fraud.provider", analysisResult.Provider);
            activity?.SetTag("processing.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("fraud.risk_factors_count", analysisResult.RiskFactors.Count);
        }
        catch (FraudDetectionServiceException ex)
        {
            logger.LogError(ex,
                "Fraud detection service error for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                command.PaymentId, command.CorrelationId);

            // Send failure event with fallback risk assessment
            var fallbackEvent = CreateFallbackFraudDetectionEvent(command, ex, stopwatch.Elapsed);
            await messageBus.PublishAsync(fallbackEvent);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "FraudDetectionServiceException");
            activity?.SetTag("processing.duration_ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error during fraud detection for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                command.PaymentId, command.CorrelationId);

            // Send critical failure event
            var criticalFailureEvent = CreateCriticalFailureFraudDetectionEvent(command, ex, stopwatch.Elapsed);
            await messageBus.PublishAsync(criticalFailureEvent);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("processing.duration_ms", stopwatch.ElapsedMilliseconds);

            // Don't rethrow - we've published a failure event to continue the saga
        }
    }

    /// <summary>
    /// Creates a fallback fraud detection event when the primary service fails.
    /// Uses conservative risk assessment to ensure payment security.
    /// </summary>
    private static FraudDetectionCompletedEvent CreateFallbackFraudDetectionEvent(
        PerformFraudDetectionCommand command,
        FraudDetectionServiceException ex,
        TimeSpan processingDuration)
    {
        // Conservative fallback: treat as medium risk when service is unavailable
        var fallbackRiskLevel = RiskLevel.Medium;
        var fallbackRiskScore = 0.5m; // Neutral score
        
        // Adjust based on amount - higher amounts get higher risk in fallback
        if (command.Amount > 1m)
        {
            fallbackRiskLevel = RiskLevel.High;
            fallbackRiskScore = 0.75m;
        }
        else if (command.Amount > 10m)
        {
            fallbackRiskLevel = RiskLevel.Blocked;
            fallbackRiskScore = 0.95m;
        }

        return new FraudDetectionCompletedEvent
        {
            PaymentId = command.PaymentId,
            RiskLevel = fallbackRiskLevel,
            RiskScore = fallbackRiskScore,
            Provider = "FallbackProvider",
            AnalyzedAt = DateTimeOffset.UtcNow,
            RiskFactors = new List<string> 
            { 
                "Primary fraud detection service unavailable",
                $"Fallback assessment based on amount: {command.Amount} {command.Currency}",
                ex.Message
            },
            Recommendations = new List<string> 
            { 
                "Manual review recommended due to service unavailability",
                "Consider alternative verification methods"
            },
            PaymentProcessingSagaId = command.PaymentProcessingSagaId,
            ProcessingDuration = processingDuration,
            AnalysisDetails = new FraudAnalysisDetails
            {
                RuleScores = new Dictionary<string, decimal> 
                { 
                    { "fallback_amount_rule", fallbackRiskScore },
                    { "service_unavailable_penalty", 0.3m }
                },
                ConfidenceLevel = 0.3m, // Low confidence in fallback assessment
                ProcessingMetadata = new Dictionary<string, object>
                {
                    { "is_fallback", true },
                    { "original_error", ex.Message },
                    { "fallback_reason", "Primary fraud detection service failure" }
                }
            }
        };
    }

    /// <summary>
    /// Creates a critical failure event when fraud detection cannot be performed.
    /// Routes payment to manual review for safety.
    /// </summary>
    private static FraudDetectionCompletedEvent CreateCriticalFailureFraudDetectionEvent(
        PerformFraudDetectionCommand command,
        Exception ex,
        TimeSpan processingDuration)
    {
        return new FraudDetectionCompletedEvent
        {
            PaymentId = command.PaymentId,
            RiskLevel = RiskLevel.High, // Conservative: route to manual review
            RiskScore = 0.8m, // High risk score for safety
            Provider = "CriticalFailureHandler",
            AnalyzedAt = DateTimeOffset.UtcNow,
            RiskFactors = new List<string> 
            { 
                "Critical fraud detection system failure",
                "Unable to perform risk assessment",
                ex.Message
            },
            Recommendations = new List<string> 
            { 
                "URGENT: Manual review required due to system failure",
                "Escalate to fraud prevention team",
                "Consider payment hold until manual review"
            },
            PaymentProcessingSagaId = command.PaymentProcessingSagaId,
            ProcessingDuration = processingDuration,
            AnalysisDetails = new FraudAnalysisDetails
            {
                RuleScores = new Dictionary<string, decimal> 
                { 
                    { "critical_failure_rule", 0.8m }
                },
                ConfidenceLevel = 0.0m, // No confidence due to system failure
                ProcessingMetadata = new Dictionary<string, object>
                {
                    { "is_critical_failure", true },
                    { "error_type", ex.GetType().Name },
                    { "error_message", ex.Message },
                    { "requires_manual_intervention", true }
                }
            }
        };
    }
}

/// <summary>
/// Fraud detection service interface for dependency injection and testing.
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    /// Performs comprehensive fraud analysis on a payment request.
    /// </summary>
    Task<FraudAnalysisResult> AnalyzePaymentAsync(
        PerformFraudDetectionCommand command, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of fraud detection analysis.
/// </summary>
public record FraudAnalysisResult
{
    public required RiskLevel RiskLevel { get; init; }
    public required decimal RiskScore { get; init; }
    public required string Provider { get; init; }
    public List<string> RiskFactors { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public FraudAnalysisDetails? AnalysisDetails { get; init; }
}

/// <summary>
/// Exception thrown by fraud detection services.
/// </summary>
public class FraudDetectionServiceException : Exception
{
    public string? Provider { get; }
    public string? ErrorCode { get; }
    public Dictionary<string, object>? ErrorDetails { get; }

    public FraudDetectionServiceException(string message, string? provider = null) 
        : base(message)
    {
        Provider = provider;
    }

    public FraudDetectionServiceException(string message, Exception innerException, string? provider = null) 
        : base(message, innerException)
    {
        Provider = provider;
    }

    public FraudDetectionServiceException(
        string message, 
        string? provider, 
        string? errorCode, 
        Dictionary<string, object>? errorDetails = null) 
        : base(message)
    {
        Provider = provider;
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
    }
}

/// <summary>
/// Mock implementation of fraud detection service for demonstration and testing.
/// In a real system, this would integrate with actual fraud detection providers like:
/// - Stripe Radar
/// - PayPal Fraud Protection
/// - Amazon Fraud Detector
/// - Custom ML models
/// - Rules engines
/// </summary>
public class MockFraudDetectionService : IFraudDetectionService
{
    private readonly ILogger<MockFraudDetectionService> _logger;
    private readonly Random _random = new();

    public MockFraudDetectionService(ILogger<MockFraudDetectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FraudAnalysisResult> AnalyzePaymentAsync(
        PerformFraudDetectionCommand command, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("MockFraudDetection.Analysis");
        
        // Simulate processing time
        await Task.Delay(_random.Next(100, 500), cancellationToken);

        // Mock analysis based on payment characteristics
        var (riskLevel, riskScore, riskFactors, recommendations) = AnalyzePaymentCharacteristics(command);

        _logger.LogDebug(
            "Mock fraud analysis completed for payment {PaymentId} - Risk: {RiskLevel}, Score: {RiskScore}",
            command.PaymentId, riskLevel, riskScore);

        return new FraudAnalysisResult
        {
            RiskLevel = riskLevel,
            RiskScore = riskScore,
            Provider = "MockFraudProvider",
            RiskFactors = riskFactors,
            Recommendations = recommendations,
            AnalysisDetails = CreateMockAnalysisDetails(command, riskScore)
        };
    }

    private (RiskLevel riskLevel, decimal riskScore, List<string> riskFactors, List<string> recommendations) 
        AnalyzePaymentCharacteristics(PerformFraudDetectionCommand command)
    {
        var riskFactors = new List<string>();
        var recommendations = new List<string>();
        decimal riskScore = 0.1m; // Base low risk

        // Amount-based risk assessment
        if (command.Amount > 10000m)
        {
            riskScore += 0.4m;
            riskFactors.Add("High transaction amount");
            recommendations.Add("Consider additional verification for high-value transactions");
        }
        else if (command.Amount > 1000m)
        {
            riskScore += 0.2m;
            riskFactors.Add("Medium transaction amount");
        }

        // Currency-based risk (mock rules)
        if (command.Currency is not ("USD" or "EUR" or "GBP"))
        {
            riskScore += 0.1m;
            riskFactors.Add("Non-major currency");
        }

        // Payment method risk
        if (command.PaymentMethod.Contains("prepaid", StringComparison.OrdinalIgnoreCase))
        {
            riskScore += 0.3m;
            riskFactors.Add("Prepaid card usage");
            recommendations.Add("Enhanced verification for prepaid cards");
        }

        // Note: Current contract doesn't include GeographicData or DeviceFingerprint
        // In a real implementation, these would be added to the command contract

        // Ensure score doesn't exceed 1.0
        riskScore = Math.Min(riskScore, 1.0m);

        // Determine risk level
        var riskLevel = riskScore switch
        {
            >= 0.8m => RiskLevel.Blocked,
            >= 0.6m => RiskLevel.High,
            >= 0.3m => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        // Add default recommendations
        if (riskLevel == RiskLevel.Low)
        {
            recommendations.Add("Proceed with standard processing");
        }
        else if (riskLevel == RiskLevel.Medium)
        {
            recommendations.Add("Consider additional authentication");
        }
        else if (riskLevel == RiskLevel.High)
        {
            recommendations.Add("Manual review recommended");
        }
        else
        {
            recommendations.Add("Block transaction - high fraud risk");
        }

        return (riskLevel, riskScore, riskFactors, recommendations);
    }

    private FraudAnalysisDetails CreateMockAnalysisDetails(
        PerformFraudDetectionCommand command, 
        decimal finalRiskScore)
    {
        return new FraudAnalysisDetails
        {
            RuleScores = new Dictionary<string, decimal>
            {
                { "amount_rule", Math.Min(command.Amount / 10000m, 0.4m) },
                { "currency_rule", command.Currency is ("USD" or "EUR" or "GBP") ? 0.0m : 0.1m },
                { "payment_method_rule", command.PaymentMethod.Contains("prepaid") ? 0.3m : 0.0m }
            },
            ModelPredictions = new Dictionary<string, decimal>
            {
                { "ml_model_v1", finalRiskScore + ((decimal)_random.NextSingle() * 0.1m - 0.05m) },
                { "rules_engine", finalRiskScore }
            },
            VelocityResults = new VelocityCheckResults
            {
                TransactionsLastHour = _random.Next(0, 5),
                TransactionsLastDay = _random.Next(0, 20),
                AmountLastHour = command.Amount,
                AmountLastDay = command.Amount * _random.Next(1, 10),
                VelocityLimitsExceeded = false,
                TriggeredRules = new List<string>()
            },
            BehavioralResults = new BehavioralAnalysisResults
            {
                TypicalityScore = 0.8m - (finalRiskScore * 0.5m),
                SpendingPatternDeviation = finalRiskScore * 0.6m,
                TimePatternScore = 0.9m,
                MerchantCategoryScore = 0.8m,
                GeographicPatternScore = 0.9m, // Default to good pattern
                DetectedAnomalies = finalRiskScore > 0.5m ? new List<string> { "Unusual transaction pattern" } : new List<string>()
            },
            ExternalProviderResponses = new List<ExternalProviderResponse>
            {
                new ExternalProviderResponse
                {
                    ProviderName = "MockProvider1",
                    RiskScore = finalRiskScore,
                    Decision = finalRiskScore > 0.5m ? "REVIEW" : "APPROVE",
                    ResponseTime = DateTimeOffset.UtcNow,
                    ProcessingDuration = TimeSpan.FromMilliseconds(_random.Next(100, 300)),
                    IsSuccessful = true
                }
            },
            ConfidenceLevel = 0.85m,
            ProcessingMetadata = new Dictionary<string, object>
            {
                { "provider", "MockFraudProvider" },
                { "version", "1.0" },
                { "processed_at", DateTimeOffset.UtcNow },
                { "is_mock", true }
            }
        };
    }

    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.FraudDetection.Mock");
}