using System.Diagnostics;
using Marten;
using Mediso.PaymentSample.Application.Modules.Payments.Ports.Secondary;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Infrastructure.Repositories;

/// <summary>
/// Marten-based implementation of IPaymentRepository.
/// Provides event sourcing capabilities with optional snapshots for payment aggregates.
/// </summary>
public sealed class MartenPaymentRepository : IPaymentRepository
{
    private static readonly ActivitySource ActivitySource = new(TracingConstants.InfrastructureServiceName);
    
    private readonly IDocumentSession _session;
    private readonly ILogger<MartenPaymentRepository> _logger;

    public MartenPaymentRepository(IDocumentSession session, ILogger<MartenPaymentRepository> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a payment aggregate by ID using event sourcing.
    /// Optionally supports point-in-time queries with asOfDate.
    /// </summary>
    public async Task<Payment?> GetByIdAsync(
        PaymentId paymentId, 
        DateTimeOffset? asOfDate = null, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentRepository.GetById");
        activity?.SetTag(TracingConstants.Tags.PaymentId, paymentId.ToString());
        activity?.SetTag("repository.method", "GetByIdAsync");
        
        if (asOfDate.HasValue)
        {
            activity?.SetTag("repository.as_of_date", asOfDate.Value.ToString("O"));
        }

        try
        {
            _logger.LogDebug("Retrieving payment {PaymentId} from event store", paymentId);

            Payment? payment;
            
            if (asOfDate.HasValue)
            {
                // Point-in-time query using event sourcing
                payment = await _session.Events.AggregateStreamAsync<Payment>(
                    paymentId.Value, 
                    timestamp: asOfDate.Value,
                    token: cancellationToken);
            }
            else
            {
                // Current state query
                payment = await _session.Events.AggregateStreamAsync<Payment>(
                    paymentId.Value, 
                    token: cancellationToken);
            }

            if (payment != null)
            {
                _logger.LogDebug("Payment {PaymentId} retrieved successfully with state {State}", 
                    paymentId, payment.State);
            }
            else
            {
                _logger.LogDebug("Payment {PaymentId} not found", paymentId);
            }

            activity?.SetTag("payment.found", payment != null);
            activity?.SetTag("payment.state", payment?.State.ToString());

            return payment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve payment {PaymentId}", paymentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Saves payment aggregate using event sourcing with optimistic concurrency control.
    /// </summary>
    public async Task SaveAsync(
        Payment payment, 
        int? expectedVersion = null, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentRepository.Save");
        activity?.SetTag(TracingConstants.Tags.PaymentId, payment.Id.ToString());
        activity?.SetTag("repository.method", "SaveAsync");
        activity?.SetTag("payment.state", payment.State.ToString());
        
        if (expectedVersion.HasValue)
        {
            activity?.SetTag("repository.expected_version", expectedVersion.Value);
        }

        try
        {
            var uncommittedEvents = payment.GetUncommittedEvents();
            var eventCount = uncommittedEvents?.Count() ?? 0;
            
            _logger.LogDebug("Saving payment {PaymentId} with {EventCount} uncommitted events", 
                payment.Id, eventCount);

            activity?.SetTag("events.count", eventCount);

            if (eventCount == 0)
            {
                _logger.LogDebug("No uncommitted events for payment {PaymentId}, skipping save", payment.Id);
                return;
            }

            // Save events to the event stream
            if (expectedVersion.HasValue)
            {
                _session.Events.Append(payment.Id.Value, expectedVersion.Value, uncommittedEvents!);
            }
            else
            {
                _session.Events.Append(payment.Id.Value, uncommittedEvents!);
            }

            // Commit the transaction
            await _session.SaveChangesAsync(cancellationToken);
            
            // Mark events as committed
            payment.MarkEventsAsCommitted();

            _logger.LogInformation("Payment {PaymentId} saved successfully with {EventCount} events", 
                payment.Id, eventCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save payment {PaymentId}", payment.Id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Checks if a payment exists without loading the full aggregate.
    /// Uses Marten's stream existence check for optimal performance.
    /// </summary>
    public async Task<bool> ExistsAsync(
        PaymentId paymentId, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentRepository.Exists");
        activity?.SetTag(TracingConstants.Tags.PaymentId, paymentId.ToString());
        activity?.SetTag("repository.method", "ExistsAsync");

        try
        {
            _logger.LogDebug("Checking existence of payment {PaymentId}", paymentId);

            // Check if event stream exists
            var streamState = await _session.Events.FetchStreamStateAsync(paymentId.Value, cancellationToken);
            var exists = streamState != null;

            _logger.LogDebug("Payment {PaymentId} exists: {Exists}", paymentId, exists);
            
            activity?.SetTag("payment.exists", exists);
            
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of payment {PaymentId}", paymentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets current payment status without loading the full aggregate.
    /// Uses projection or latest event analysis for optimal performance.
    /// </summary>
    public async Task<PaymentStatus?> GetStatusAsync(
        PaymentId paymentId, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PaymentRepository.GetStatus");
        activity?.SetTag(TracingConstants.Tags.PaymentId, paymentId.ToString());
        activity?.SetTag("repository.method", "GetStatusAsync");

        try
        {
            _logger.LogDebug("Retrieving status for payment {PaymentId}", paymentId);

            // For now, we'll load the aggregate to get the status
            // In a more optimized scenario, you could use read model projections
            var payment = await GetByIdAsync(paymentId, cancellationToken: cancellationToken);
            
            if (payment == null)
            {
                _logger.LogDebug("Payment {PaymentId} not found", paymentId);
                return null;
            }

            var status = (PaymentStatus)payment.State;
            
            _logger.LogDebug("Payment {PaymentId} has status {Status}", paymentId, status);
            
            activity?.SetTag("payment.status", status.ToString());
            
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for payment {PaymentId}", paymentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}