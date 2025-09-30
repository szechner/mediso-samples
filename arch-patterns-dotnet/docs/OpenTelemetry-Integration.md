# OpenTelemetry Integration Guide

## Overview

This document describes the comprehensive OpenTelemetry integration added to the Mediso Payment Sample application, including SQL and Marten event store observability.

## Features Added

### 🔍 **Tracing**
- **SQL Queries**: All PostgreSQL queries are traced via `AddSqlClientInstrumentation()` and `AddNpgsql()`
- **Marten Operations**: Event store operations (append, load, snapshot) are traced with custom activity sources
- **HTTP Requests**: ASP.NET Core request/response tracing with enriched attributes
- **Custom Business Operations**: Payment creation, AML checks, funds reservation, etc.

### 📊 **Metrics**
- **Business Metrics**: 
  - Payment counts by currency and status
  - Payment amounts processed 
  - AML check results
  - Fund reservation operations
- **Event Store Metrics**:
  - Events appended per stream type
  - Snapshot creation frequency
  - Operation duration histograms
- **System Metrics**:
  - ASP.NET Core request metrics
  - Runtime performance metrics
  - HTTP client metrics

### 🏷️ **Tags & Attributes**
- Payment amount and currency
- Stream types and versions
- Operation success/failure
- SQL statement details
- Request correlation IDs

## Configuration

### API Project (`Mediso.PaymentSample.Api`)

The API is configured with comprehensive OpenTelemetry instrumentation:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Marten")           // Marten event store operations
        .AddSource("Npgsql")           // PostgreSQL database operations
        .AddAspNetCoreInstrumentation() // HTTP request tracing
        .AddHttpClientInstrumentation() // Outbound HTTP calls
        .AddSqlClientInstrumentation()  // SQL query tracing
        .AddNpgsql()                   // PostgreSQL-specific tracing
        .AddJaegerExporter()           // Export to Jaeger
        .AddOtlpExporter())            // OTLP export for production
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation() // HTTP metrics
        .AddRuntimeInstrumentation()    // .NET runtime metrics
        .AddMeter("Mediso.PaymentSample.*") // Custom business metrics
        .AddOtlpExporter());           // OTLP export for production
```

### Infrastructure Project (`Mediso.PaymentSample.Infrastructure`)

#### Custom PaymentMetrics Class

A dedicated metrics class tracks business-specific operations:

```csharp
public class PaymentMetrics
{
    // Counters
    - payment.created.total
    - payment.settled.total
    - payment.failed.total
    - eventstore.events_appended.total
    - eventstore.snapshots_created.total
    
    // Histograms
    - payment.amount (with currency tags)
    - eventstore.operation.duration
    - eventstore.snapshot.duration
}
```

#### Enhanced Event Store

The `SnapshotPaymentEventStore` now includes:
- Operation timing metrics
- Success/failure tracking
- Event count metrics per operation
- Snapshot creation tracking

#### Marten Configuration

Marten is configured to work seamlessly with OpenTelemetry:
- Uses Npgsql instrumentation for PostgreSQL tracing
- Custom activity sources for event store operations
- Proper correlation with HTTP request traces

## Metrics Available

### Business Metrics
| Metric | Type | Description | Tags |
|--------|------|-------------|------|
| `payment.created.total` | Counter | Total payments created | currency |
| `payment.settled.total` | Counter | Total payments settled | currency |
| `payment.failed.total` | Counter | Total payments failed | currency, reason |
| `payment.aml_check.total` | Counter | Total AML checks performed | ruleset_version, result |
| `payment.funds_reserved.total` | Counter | Total funds reservations | currency |
| `payment.amount` | Histogram | Payment amounts processed | currency, operation |

### Event Store Metrics
| Metric | Type | Description | Tags |
|--------|------|-------------|------|
| `eventstore.events_appended.total` | Counter | Events appended to store | stream_type |
| `eventstore.snapshots_created.total` | Counter | Aggregate snapshots created | aggregate_type, version |
| `eventstore.operation.duration` | Histogram | Event store operation time | operation, success |
| `eventstore.snapshot.duration` | Histogram | Snapshot operation time | operation, success |

### System Metrics
- `http.server.duration` - HTTP request duration with custom buckets
- `dotnet.*` - .NET runtime metrics
- `process.*` - Process-level metrics

## Traces Available

### SQL Traces
- **Database**: `payment_sample`
- **Operations**: SELECT, INSERT, UPDATE for events and snapshots
- **Duration**: Query execution time
- **Status**: Success/failure with error details

### Marten Traces
- **EventStore.AppendEvents**: Event persistence operations
- **EventStore.LoadAggregate**: Aggregate reconstruction
- **EventStore.SaveSnapshot**: Snapshot creation
- **EventStore.LoadSnapshot**: Snapshot loading

### HTTP Traces
- **Request Method**: GET, POST, PUT, DELETE
- **Path**: API endpoint paths (`/api/payments/*`)
- **Status**: HTTP response codes
- **Duration**: End-to-end request time

## Usage Examples

### Viewing Traces in Jaeger
1. Start Jaeger: `docker run -p 16686:16686 jaegertracing/all-in-one:latest`
2. Open http://localhost:16686
3. Select service: `Mediso.PaymentSample.Api`
4. View traces for payment operations

### Sample Trace Structure
```
HTTP POST /api/payments
├── EventStore.AppendEvents
│   ├── Npgsql query: INSERT INTO mt_events
│   └── Npgsql query: UPDATE mt_streams
├── EventStore.SaveSnapshot (if version % 5 == 0)
│   └── Npgsql query: INSERT INTO mt_doc_paymentsnapshot
└── HTTP Response 201 Created
```

### Metrics Export
Metrics are exported via OTLP to your configured observability platform (Jaeger, Prometheus, etc.).

## Configuration Files

### API appsettings.json
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=payment_sample;Username=payment_user;Password=payment_password"
  },
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": "6831",
    "Endpoint": "http://localhost:14268/api/traces"
  }
}
```

## Best Practices

### 🎯 **Tagging Strategy**
- Use consistent tag names across metrics
- Include business-relevant dimensions (currency, operation type)
- Keep cardinality reasonable (avoid high-cardinality tags like IDs)

### ⚡ **Performance**
- Metrics collection has minimal overhead
- Traces are sampled by default
- OTLP exporters are preferred for production environments

### 🔍 **Monitoring**
- Set up alerts on key business metrics (payment failure rates)
- Monitor event store performance metrics
- Track SQL query performance trends

## Troubleshooting

### Common Issues
1. **No traces appearing**: Check Jaeger endpoint configuration
2. **Missing SQL traces**: Ensure `AddNpgsql()` and `AddSqlClientInstrumentation()` are configured
3. **No custom metrics**: Verify `PaymentMetrics` is registered as singleton

### Debug Information
- Enable console exporters for development debugging
- Check activity source names match exactly
- Verify meter names in metrics configuration

## Integration with Monitoring Stack

This OpenTelemetry setup integrates with:
- **Jaeger**: For distributed tracing
- **Prometheus**: For metrics collection (via OTLP export)
- **Grafana**: For metrics visualization
- **Azure Monitor**: For cloud deployments
- **AWS X-Ray**: For AWS deployments

## Next Steps

1. **Production Configuration**: Replace console exporters with appropriate production exporters
2. **Alerting**: Set up alerts on key metrics using your monitoring platform
3. **Dashboards**: Create business and technical dashboards
4. **Sampling**: Configure trace sampling for production volumes
5. **Log Correlation**: Connect structured logs with trace IDs