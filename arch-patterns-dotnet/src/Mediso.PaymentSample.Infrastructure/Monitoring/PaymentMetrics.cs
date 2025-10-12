using System.Diagnostics.Metrics;

namespace Mediso.PaymentSample.Infrastructure.Monitoring;

/// <summary>
/// OpenTelemetry metrics for Payment domain operations
/// </summary>
public class PaymentMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _paymentsCreated;
    private readonly Counter<long> _paymentsSettled;
    private readonly Counter<long> _paymentsFailed;
    private readonly Counter<long> _amlChecks;
    private readonly Counter<long> _fundsReserved;
    private readonly Counter<long> _eventsAppended;
    private readonly Counter<long> _snapshotsCreated;
    private readonly Histogram<double> _paymentAmount;
    private readonly Histogram<double> _eventStoreOperationDuration;
    private readonly Histogram<double> _snapshotOperationDuration;
    
    public PaymentMetrics()
    {
        _meter = new Meter("Mediso.PaymentSample.Infrastructure", "1.0.0");
        
        // Payment business metrics
        _paymentsCreated = _meter.CreateCounter<long>(
            "payment.created.total",
            description: "Total number of payments created");
            
        _paymentsSettled = _meter.CreateCounter<long>(
            "payment.settled.total", 
            description: "Total number of payments settled");
            
        _paymentsFailed = _meter.CreateCounter<long>(
            "payment.failed.total",
            description: "Total number of payments that failed");
            
        _amlChecks = _meter.CreateCounter<long>(
            "payment.aml_check.total",
            description: "Total number of AML checks performed");
            
        _fundsReserved = _meter.CreateCounter<long>(
            "payment.funds_reserved.total",
            description: "Total number of funds reservations");
            
        // Event store metrics
        _eventsAppended = _meter.CreateCounter<long>(
            "eventstore.events_appended.total",
            description: "Total number of events appended to event store");
            
        _snapshotsCreated = _meter.CreateCounter<long>(
            "eventstore.snapshots_created.total",
            description: "Total number of aggregate snapshots created");
            
        // Business value metrics
        _paymentAmount = _meter.CreateHistogram<double>(
            "payment.amount",
            description: "Payment amounts processed",
            unit: "currency");
            
        // Performance metrics
        _eventStoreOperationDuration = _meter.CreateHistogram<double>(
            "eventstore.operation.duration",
            description: "Duration of event store operations",
            unit: "ms");
            
        _snapshotOperationDuration = _meter.CreateHistogram<double>(
            "eventstore.snapshot.duration", 
            description: "Duration of snapshot operations",
            unit: "ms");
    }
    
    // Payment business events
    public void RecordPaymentCreated(string currency, double amount)
    {
        _paymentsCreated.Add(1, new KeyValuePair<string, object?>("currency", currency));
        _paymentAmount.Record(amount, 
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("operation", "create"));
    }
    
    public void RecordPaymentSettled(string currency, double amount)
    {
        _paymentsSettled.Add(1, new KeyValuePair<string, object?>("currency", currency));
        _paymentAmount.Record(amount,
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("operation", "settle"));
    }
    
    public void RecordPaymentFailed(string currency, double amount, string reason)
    {
        _paymentsFailed.Add(1, 
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("reason", reason));
        _paymentAmount.Record(amount,
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("operation", "fail"));
    }
    
    public void RecordAMLCheck(string rulesetVersion, bool passed)
    {
        _amlChecks.Add(1,
            new KeyValuePair<string, object?>("ruleset_version", rulesetVersion),
            new KeyValuePair<string, object?>("result", passed ? "passed" : "failed"));
    }
    
    public void RecordFundsReserved(string currency, double amount)
    {
        _fundsReserved.Add(1, new KeyValuePair<string, object?>("currency", currency));
    }
    
    // Event store events  
    public void RecordEventsAppended(int eventCount, string streamType)
    {
        _eventsAppended.Add(eventCount,
            new KeyValuePair<string, object?>("stream_type", streamType));
    }
    
    public void RecordSnapshotCreated(string aggregateType, long version)
    {
        _snapshotsCreated.Add(1,
            new KeyValuePair<string, object?>("aggregate_type", aggregateType),
            new KeyValuePair<string, object?>("version", version));
    }
    
    // Performance events
    public void RecordEventStoreOperation(string operation, double durationMs, bool success)
    {
        _eventStoreOperationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", success));
    }
    
    public void RecordSnapshotOperation(string operation, double durationMs, bool success)
    {
        _snapshotOperationDuration.Record(durationMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", success));
    }
    
    public void Dispose()
    {
        _meter.Dispose();
    }
}