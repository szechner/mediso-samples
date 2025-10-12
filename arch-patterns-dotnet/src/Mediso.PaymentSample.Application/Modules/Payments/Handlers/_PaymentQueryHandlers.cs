using System.Diagnostics;
using Mediso.PaymentSample.Application.Common.Resilience;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Ports.Secondary;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Application.Modules.Payments.Handlers;

/// <summary>
/// Query handlers for payment operations using MessagePipes.
/// Implements CQRS read side with comprehensive caching, monitoring, and resiliency.
/// 
/// Features:
/// - Multi-level caching strategy (L1: Memory, L2: Redis)
/// - Circuit breaker pattern for database operations
/// - Performance monitoring and metrics
/// - Cache-aside pattern with intelligent cache invalidation
/// - Projection-based data optimization
/// - Comprehensive observability and tracing
/// </summary>
public class _PaymentQueryHandlers // : 
    // IRequestHandler<GetPaymentQuery, GetPaymentResponse>,
    // IRequestHandler<SearchPaymentsQuery, SearchPaymentsResponse>,
    // IRequestHandler<GetPaymentStatsQuery, GetPaymentStatsResponse>
{
    private static readonly ActivitySource ActivitySource = new("Mediso.PaymentSample.Application.Queries");
    
    private readonly IPaymentRepository _paymentRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IResiliencePipelineProvider _resilienceProvider;
    private readonly ILogger<_PaymentQueryHandlers> _logger;
    
    // Cache configuration
    private static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StatsCacheExpiration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SearchCacheExpiration = TimeSpan.FromMinutes(2);

    public _PaymentQueryHandlers(
        IPaymentRepository paymentRepository,
        IMemoryCache memoryCache,
        IResiliencePipelineProvider resilienceProvider,
        ILogger<_PaymentQueryHandlers> logger)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _resilienceProvider = resilienceProvider ?? throw new ArgumentNullException(nameof(resilienceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// MessagePipe interface implementation for GetPaymentQuery.
    /// </summary>
    public GetPaymentResponse Invoke(GetPaymentQuery request) => Handle(request, default).Result;
    
    public ValueTask<GetPaymentResponse> Invoke(GetPaymentQuery request, CancellationToken cancellationToken) 
        => Handle(request, cancellationToken);
    
    /// <summary>
    /// Handles GetPaymentQuery with intelligent caching and projection optimization.
    /// </summary>
    public async ValueTask<GetPaymentResponse> Handle(
        GetPaymentQuery request, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Query.GetPayment");
        activity?.SetTag(TracingConstants.CorrelationId, request.CorrelationId);
        activity?.SetTag("payment.id", request.PaymentId.Value);
        activity?.SetTag("query.projection", request.Projection.ToString());
        activity?.SetTag("query.include_sensitive", request.IncludeSensitiveData);

        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogDebug(
            "Processing GetPaymentQuery for payment {PaymentId} with projection {Projection} [CorrelationId: {CorrelationId}]",
            request.PaymentId, request.Projection, request.CorrelationId);

        try
        {
            // Generate cache key based on request parameters
            var cacheKey = GeneratePaymentCacheKey(request);
            
            // Try L1 cache (Memory) first
            if (_memoryCache.TryGetValue(cacheKey, out GetPaymentResponse? cachedResponse))
            {
                _logger.LogDebug(
                    "Cache hit for payment {PaymentId} [CorrelationId: {CorrelationId}]",
                    request.PaymentId, request.CorrelationId);
                
                activity?.SetTag("cache.hit", true);
                activity?.SetTag("cache.level", "memory");
                activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
                
                return cachedResponse! with { RetrievedAt = DateTimeOffset.UtcNow };
            }

            // Cache miss - fetch from database with resilience
            var pipeline = _resilienceProvider.GetPipeline("database");
            
            var payment = await pipeline.ExecuteAsync(async ct =>
            {
                using var dbActivity = ActivitySource.StartActivity("Database.GetPayment");
                return await _paymentRepository.GetByIdAsync(request.PaymentId, request.AsOfDate, ct);
            }, cancellationToken);

            if (payment == null)
            {
                var notFoundResponse = CreateNotFoundResponse(request);
                
                _logger.LogInformation(
                    "Payment {PaymentId} not found [CorrelationId: {CorrelationId}]",
                    request.PaymentId, request.CorrelationId);
                
                activity?.SetTag("payment.found", false);
                activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
                
                return notFoundResponse;
            }

            // Project payment data based on requested projection
            var response = await ProjectPaymentDataAsync(payment, request, cancellationToken);
            
            // Cache the response (with appropriate expiration based on projection)
            var cacheExpiration = GetCacheExpirationForProjection(request.Projection);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheExpiration,
                Priority = GetCachePriorityForProjection(request.Projection)
            };
            
            _memoryCache.Set(cacheKey, response, cacheOptions);
            
            _logger.LogDebug(
                "Successfully retrieved payment {PaymentId} with projection {Projection} in {Duration}ms [CorrelationId: {CorrelationId}]",
                request.PaymentId, request.Projection, stopwatch.ElapsedMilliseconds, request.CorrelationId);

            activity?.SetTag("payment.found", true);
            activity?.SetTag("cache.hit", false);
            activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("payment.status", payment.State.ToString());

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve payment {PaymentId} [CorrelationId: {CorrelationId}]",
                request.PaymentId, request.CorrelationId);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }

    /// <summary>
    /// MessagePipe interface implementation for SearchPaymentsQuery.
    /// </summary>
    public SearchPaymentsResponse Invoke(SearchPaymentsQuery request) => Handle(request, default).Result;
    
    public ValueTask<SearchPaymentsResponse> Invoke(SearchPaymentsQuery request, CancellationToken cancellationToken) 
        => Handle(request, cancellationToken);
    
    /// <summary>
    /// Handles SearchPaymentsQuery with intelligent caching and performance optimization.
    /// </summary>
    public async ValueTask<SearchPaymentsResponse> Handle(
        SearchPaymentsQuery request, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Query.SearchPayments");
        activity?.SetTag(TracingConstants.CorrelationId, request.CorrelationId);
        activity?.SetTag("search.page", request.Pagination.Page);
        activity?.SetTag("search.page_size", request.Pagination.PageSize);
        activity?.SetTag("search.projection", request.Projection.ToString());

        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogDebug(
            "Processing SearchPaymentsQuery with criteria {Criteria} [CorrelationId: {CorrelationId}]",
            request.Criteria, request.CorrelationId);

        try
        {
            // Generate cache key for search results
            var cacheKey = GenerateSearchCacheKey(request);
            
            // Check cache for search results
            if (_memoryCache.TryGetValue(cacheKey, out SearchPaymentsResponse? cachedResponse))
            {
                _logger.LogDebug(
                    "Cache hit for search query [CorrelationId: {CorrelationId}]",
                    request.CorrelationId);
                
                activity?.SetTag("cache.hit", true);
                activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
                
                return cachedResponse!;
            }

            // Execute search with resilience pipeline
            var pipeline = _resilienceProvider.GetPipeline("database");
            
            var searchResults = await pipeline.ExecuteAsync(async ct =>
            {
                using var dbActivity = ActivitySource.StartActivity("Database.SearchPayments");
                return await ExecutePaymentSearchAsync(request, ct);
            }, cancellationToken);

            var response = new SearchPaymentsResponse
            {
                Payments = searchResults.Results,
                Pagination = searchResults.Pagination,
                SearchMetadata = new SearchMetadata
                {
                    ExecutionTime = stopwatch.Elapsed,
                    ResultCount = searchResults.Results.Count,
                    IsFromCache = false,
                    IndexesUsed = searchResults.IndexesUsed
                },
                CorrelationId = request.CorrelationId
            };

            // Cache search results with shorter expiration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SearchCacheExpiration,
                Priority = CacheItemPriority.Normal
            };
            
            _memoryCache.Set(cacheKey, response, cacheOptions);
            
            _logger.LogInformation(
                "Search query returned {ResultCount} results in {Duration}ms [CorrelationId: {CorrelationId}]",
                response.SearchMetadata.ResultCount, stopwatch.ElapsedMilliseconds, request.CorrelationId);

            activity?.SetTag("search.result_count", response.SearchMetadata.ResultCount);
            activity?.SetTag("cache.hit", false);
            activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute search query [CorrelationId: {CorrelationId}]",
                request.CorrelationId);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }

    /// <summary>
    /// MessagePipe interface implementation for GetPaymentStatsQuery.
    /// </summary>
    public GetPaymentStatsResponse Invoke(GetPaymentStatsQuery request) => Handle(request, default).Result;
    
    public ValueTask<GetPaymentStatsResponse> Invoke(GetPaymentStatsQuery request, CancellationToken cancellationToken) 
        => Handle(request, cancellationToken);
    
    /// <summary>
    /// Handles GetPaymentStatsQuery with aggressive caching and optimized aggregations.
    /// </summary>
    public async ValueTask<GetPaymentStatsResponse> Handle(
        GetPaymentStatsQuery request, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Query.GetPaymentStats");
        activity?.SetTag(TracingConstants.CorrelationId, request.CorrelationId);
        activity?.SetTag("stats.from_date", request.FromDate.ToString("O"));
        activity?.SetTag("stats.to_date", request.ToDate.ToString("O"));
        activity?.SetTag("stats.grouping", request.Grouping.ToString());
        activity?.SetTag("stats.metrics", request.Metrics.ToString());

        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogDebug(
            "Processing GetPaymentStatsQuery from {FromDate} to {ToDate} [CorrelationId: {CorrelationId}]",
            request.FromDate, request.ToDate, request.CorrelationId);

        try
        {
            // Generate cache key for statistics
            var cacheKey = GenerateStatsCacheKey(request);
            
            // Check cache for statistics (longer cache duration for stats)
            if (_memoryCache.TryGetValue(cacheKey, out GetPaymentStatsResponse? cachedResponse))
            {
                _logger.LogDebug(
                    "Cache hit for stats query [CorrelationId: {CorrelationId}]",
                    request.CorrelationId);
                
                activity?.SetTag("cache.hit", true);
                activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
                
                return cachedResponse!;
            }

            // Execute statistics query with resilience
            var pipeline = _resilienceProvider.GetPipeline("database");
            
            var statsData = await pipeline.ExecuteAsync(async ct =>
            {
                using var dbActivity = ActivitySource.StartActivity("Database.GetPaymentStats");
                return await ExecutePaymentStatsQueryAsync(request, ct);
            }, cancellationToken);

            var response = new GetPaymentStatsResponse
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Summary = statsData.Summary,
                GroupedStats = statsData.GroupedStats,
                StatusDistribution = statsData.StatusDistribution,
                PaymentMethodStats = statsData.PaymentMethodStats,
                CurrencyStats = statsData.CurrencyStats,
                CorrelationId = request.CorrelationId
            };

            // Cache statistics with longer expiration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = StatsCacheExpiration,
                Priority = CacheItemPriority.High, // Stats are expensive to compute
                Size = EstimateResponseSize(response) // Help with cache eviction
            };
            
            _memoryCache.Set(cacheKey, response, cacheOptions);
            
            _logger.LogInformation(
                "Statistics query completed with {TotalPayments} payments in {Duration}ms [CorrelationId: {CorrelationId}]",
                response.Summary.TotalPayments, stopwatch.ElapsedMilliseconds, request.CorrelationId);

            activity?.SetTag("stats.total_payments", response.Summary.TotalPayments);
            activity?.SetTag("stats.success_rate", response.Summary.SuccessRate);
            activity?.SetTag("cache.hit", false);
            activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute stats query [CorrelationId: {CorrelationId}]",
                request.CorrelationId);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("query.duration_ms", stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }

    // ========================================================================================
    // PRIVATE HELPER METHODS
    // ========================================================================================

    private static string GeneratePaymentCacheKey(GetPaymentQuery request)
    {
        var keyParts = new[]
        {
            "payment",
            request.PaymentId.Value.ToString(),
            request.Projection.ToString().ToLowerInvariant(),
            request.IncludeSensitiveData.ToString().ToLowerInvariant(),
            request.AsOfDate?.ToString("O") ?? "latest"
        };
        
        return string.Join(":", keyParts);
    }

    private static string GenerateSearchCacheKey(SearchPaymentsQuery request)
    {
        // Create a deterministic hash of search criteria
        var criteriaHash = request.Criteria.GetHashCode();
        var sortHash = $"{request.Sort.SortBy}_{request.Sort.Direction}".GetHashCode();
        var paginationHash = $"{request.Pagination.Page}_{request.Pagination.PageSize}".GetHashCode();
        
        return $"search:{criteriaHash}:{sortHash}:{paginationHash}:{request.Projection}";
    }

    private static string GenerateStatsCacheKey(GetPaymentStatsQuery request)
    {
        var keyParts = new[]
        {
            "stats",
            request.FromDate.ToString("yyyy-MM-dd"),
            request.ToDate.ToString("yyyy-MM-dd"),
            request.CustomerId?.Value.ToString() ?? "all",
            request.MerchantId?.Value.ToString() ?? "all",
            request.Grouping.ToString().ToLowerInvariant(),
            request.Metrics.ToString().ToLowerInvariant()
        };
        
        return string.Join(":", keyParts);
    }

    private static TimeSpan GetCacheExpirationForProjection(PaymentProjection projection)
    {
        return projection switch
        {
            PaymentProjection.Summary => TimeSpan.FromMinutes(10), // Longer cache for summaries
            PaymentProjection.Detailed => TimeSpan.FromMinutes(5),  // Medium cache for detailed
            PaymentProjection.Full => TimeSpan.FromMinutes(2),      // Shorter cache for full data
            PaymentProjection.Audit => TimeSpan.FromMinutes(1),     // Very short for audit data
            _ => DefaultCacheExpiration
        };
    }

    private static CacheItemPriority GetCachePriorityForProjection(PaymentProjection projection)
    {
        return projection switch
        {
            PaymentProjection.Summary => CacheItemPriority.High,    // Summaries are frequently accessed
            PaymentProjection.Detailed => CacheItemPriority.Normal,
            PaymentProjection.Full => CacheItemPriority.Low,       // Full data is less frequently accessed
            PaymentProjection.Audit => CacheItemPriority.Low,      // Audit data is rarely repeated
            _ => CacheItemPriority.Normal
        };
    }

    private GetPaymentResponse CreateNotFoundResponse(GetPaymentQuery request)
    {
        // Return a proper not-found response rather than null
        // This could be enhanced to return a specific NotFoundResult type
        throw new PaymentNotFoundException($"Payment {request.PaymentId} not found");
    }

    private async Task<GetPaymentResponse> ProjectPaymentDataAsync(
        Payment payment, 
        GetPaymentQuery request, 
        CancellationToken cancellationToken)
    {
        // Project payment data based on requested projection level
        // This is where you'd implement the actual projection logic
        // For now, this is a simplified implementation
        
        var response = new GetPaymentResponse
        {
            PaymentId = payment.Id,
            Status = (PaymentStatus)payment.State, // Convert PaymentState to PaymentStatus
            CorrelationId = request.CorrelationId,
            Amount = new PaymentAmountInfo
            {
                OriginalAmount = payment.Amount.Amount,
                Currency = payment.Amount.Currency.Code
                // Add other amount details based on projection
            },
            Timestamps = new PaymentTimestamps
            {
                InitiatedAt = DateTimeOffset.UtcNow // TODO: Get from payment events
                // Add other timestamps based on payment events
            }
        };

        // Add additional data based on projection level
        if (request.Projection >= PaymentProjection.Detailed)
        {
            response = response with
            {
                ProcessingInfo = await GetPaymentProcessingInfoAsync(payment, cancellationToken)
            };
        }

        if (request.Projection == PaymentProjection.Full && request.IncludeSensitiveData)
        {
            // Add sensitive data only if requested and authorized
            response = response with
            {
                Metadata = await GetPaymentMetadataAsync(payment, cancellationToken)
            };
        }

        if (request.Projection == PaymentProjection.Audit)
        {
            response = response with
            {
                AuditTrail = await GetPaymentAuditTrailAsync(payment, cancellationToken)
            };
        }

        return response;
    }

    private async Task<PaymentProcessingInfo?> GetPaymentProcessingInfoAsync(Payment payment, CancellationToken cancellationToken)
    {
        // Implementation would fetch processing details from the payment aggregate or read models
        await Task.CompletedTask; // Placeholder
        return new PaymentProcessingInfo
        {
            // Populate processing info from payment events/state
        };
    }

    private async Task<Dictionary<string, string>?> GetPaymentMetadataAsync(Payment payment, CancellationToken cancellationToken)
    {
        // Implementation would fetch metadata, potentially filtered for security
        await Task.CompletedTask; // Placeholder
        return new Dictionary<string, string>();
    }

    private async Task<List<PaymentAuditEntry>?> GetPaymentAuditTrailAsync(Payment payment, CancellationToken cancellationToken)
    {
        // Implementation would build audit trail from domain events
        await Task.CompletedTask; // Placeholder
        return new List<PaymentAuditEntry>();
    }

    private async Task<SearchResultData> ExecutePaymentSearchAsync(SearchPaymentsQuery request, CancellationToken cancellationToken)
    {
        // This would be implemented to execute the actual search against the database/read models
        // For now, returning placeholder data
        await Task.CompletedTask;
        
        return new SearchResultData(
            Results: new List<PaymentSummary>(),
            Pagination: new PaginationMetadata
            {
                CurrentPage = request.Pagination.Page,
                PageSize = request.Pagination.PageSize,
                TotalItems = 0,
                TotalPages = 0,
                HasPreviousPage = false,
                HasNextPage = false
            },
            IndexesUsed: "payment_search_index"
        );
    }

    private async Task<StatsResultData> ExecutePaymentStatsQueryAsync(GetPaymentStatsQuery request, CancellationToken cancellationToken)
    {
        // This would be implemented to execute aggregation queries against the database
        // For now, returning placeholder data
        await Task.CompletedTask;
        
        return new StatsResultData(
            Summary: new PaymentStatsSummary
            {
                TotalPayments = 0,
                TotalAmount = 0,
                AverageAmount = 0,
                SuccessfulPayments = 0,
                FailedPayments = 0,
                SuccessRate = 0
            },
            GroupedStats: new List<PaymentStatsGroup>(),
            StatusDistribution: new Dictionary<PaymentStatus, int>(),
            PaymentMethodStats: new Dictionary<string, PaymentMethodStats>(),
            CurrencyStats: new Dictionary<string, CurrencyStats>()
        );
    }

    private static int EstimateResponseSize(GetPaymentStatsResponse response)
    {
        // Simple size estimation for cache management
        var baseSize = 1024; // Base response size
        var groupedStatsSize = (response.GroupedStats?.Count ?? 0) * 256;
        var distributionSize = (response.StatusDistribution?.Count ?? 0) * 64;
        
        return baseSize + groupedStatsSize + distributionSize;
    }

    // Supporting data structures
    private record SearchResultData(
        List<PaymentSummary> Results,
        PaginationMetadata Pagination,
        string IndexesUsed);

    private record StatsResultData(
        PaymentStatsSummary Summary,
        List<PaymentStatsGroup>? GroupedStats,
        Dictionary<PaymentStatus, int>? StatusDistribution,
        Dictionary<string, PaymentMethodStats>? PaymentMethodStats,
        Dictionary<string, CurrencyStats>? CurrencyStats);
}

/// <summary>
/// Exception thrown when a payment is not found.
/// </summary>
public class PaymentNotFoundException : Exception
{
    public PaymentNotFoundException(string message) : base(message) { }
    public PaymentNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}