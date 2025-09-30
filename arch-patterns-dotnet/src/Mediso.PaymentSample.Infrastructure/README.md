# Infrastructure Layer - PostgreSQL 18 + Marten Event Store Implementation

This document describes the **PostgreSQL 18** enhanced Marten-based event store implementation with advanced monitoring, structured logging, and OpenTelemetry tracing integration.

## Overview

This implementation provides a complete event sourcing infrastructure using:
- **PostgreSQL 18** with enhanced JSON performance and monitoring
- **Marten** for PostgreSQL-based event storage with optimizations
- **OpenTelemetry** for distributed tracing with PostgreSQL 18 instrumentation
- **Structured logging** with correlation IDs and database performance metrics
- **Domain event support** for all payment-related events
- **Advanced monitoring** with pgAdmin and custom PostgreSQL 18 functions
- **Performance optimization** leveraging PostgreSQL 18 features

## Components

### 1. MartenEventStore (`EventStore/MartenEventStore.cs`)

Core implementation of the `IEventStore` interface that provides:

```csharp
public interface IEventStore : IDisposable
{
    Task AppendEventsAsync(string streamId, long expectedVersion, IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(string streamId, long fromVersion = 0, CancellationToken cancellationToken = default);
    Task<T?> LoadAggregateAsync<T>(string aggregateId, CancellationToken cancellationToken = default) where T : class, IAggregateRoot;
    Task SaveAggregateAsync<T>(T aggregate, CancellationToken cancellationToken = default) where T : class, IAggregateRoot;
}
```

**Features:**
- **OpenTelemetry integration** - Creates activity spans for all operations
- **Structured logging** - Logs with context, correlation IDs, and performance metrics
- **Optimistic concurrency control** - Uses expected version for event appending
- **Event tagging** - Adds detailed tags to tracing spans for observability

### 2. MartenConfiguration (`Configuration/MartenConfiguration.cs`)

Extension method for DI registration:

```csharp
public static IServiceCollection AddMartenEventStore(this IServiceCollection services, string connectionString)
```

**Configuration includes:**
- **Event type registration** for all payment domain events
- **Database schema** setup (`payment_sample`)
- **Event metadata** configuration (correlation IDs, causation IDs, headers)
- **Service registration** for `IEventStore` and Marten components

### 3. Supported Domain Events

The implementation supports all payment-related domain events:

```csharp
// Payment lifecycle events
PaymentRequested, AMLPassed, PaymentFlagged
FundsReserved, FundsReservationFailed
PaymentJournaled, PaymentSettled
PaymentCancelled, PaymentDeclined, PaymentFailed, PaymentNotified
```

### 4. OpenTelemetry Integration

Each event store operation creates detailed activity spans:

```csharp
using var activity = ActivitySource.StartActivity("EventStore.AppendEvents");
activity?.SetTag(TracingConstants.Tags.StreamId, streamId);
activity?.SetTag(TracingConstants.Tags.ExpectedVersion, expectedVersion);
activity?.SetTag(TracingConstants.Tags.EventCount, events.Count());
```

**Available tracing tags:**
- `stream.id` - Event stream identifier
- `expected.version` / `from.version` - Version control
- `event.count` - Number of events in operation
- `aggregate.id` / `aggregate.type` / `aggregate.version` - Aggregate information
- `event.{index}.type` - Individual event type information

### 5. PostgreSQL 18 Enhanced Monitoring

Custom monitoring service (`IPostgreSql18MonitoringService`) provides:
- **Event store statistics** - table sizes, event counts, performance metrics
- **Slow query analysis** - queries over configurable thresholds with execution statistics
- **Connection monitoring** - active connections, states, and duration tracking
- **Cache hit ratios** - buffer cache and index cache performance metrics
- **Monitoring dashboard** - JSON-based comprehensive system overview

### 6. Structured Logging

All operations include structured logging with:
- **Correlation IDs** from current activity
- **Operation context** (stream ID, versions, counts)
- **Performance timing** information with PostgreSQL 18 optimizations
- **Error details** with full exception context
- **Database performance metrics** including query duration and batch operations

## Usage

### 1. PostgreSQL 18 Environment Setup

**Quick Start with PowerShell:**
```powershell
# Start PostgreSQL 18 with monitoring
.\scripts\start-postgresql18.ps1

# View logs
.\scripts\start-postgresql18.ps1 -Logs

# Clean environment
.\scripts\start-postgresql18.ps1 -Clean
```

**Access Points:**
- **PostgreSQL 18**: `localhost:5432` (payment_user/payment_password)
- **pgAdmin**: `http://localhost:8080` (admin@mediso.com/admin123)
- **Jaeger**: `http://localhost:16686`

### 2. Registration

```csharp
// In Program.cs or Startup.cs - PostgreSQL 18 optimized
services.AddMartenEventStore("Host=localhost;Database=payment_sample;Username=payment_user;Password=payment_password");
```

### 2. Basic Usage

```csharp
public class PaymentService
{
    private readonly IEventStore _eventStore;

    public async Task ProcessPayment(Payment payment)
    {
        // Save aggregate with uncommitted events
        await _eventStore.SaveAggregateAsync(payment);
        
        // Load aggregate from event stream
        var loadedPayment = await _eventStore.LoadAggregateAsync<Payment>(payment.Id);
    }
}
```

### 3. Direct Event Operations

```csharp
// Append events to a stream
var events = new IDomainEvent[] { new PaymentRequested(...) };
await _eventStore.AppendEventsAsync("payment-123", expectedVersion: 0, events);

// Read events from a stream
var streamEvents = await _eventStore.GetEventsAsync("payment-123", fromVersion: 0);
```

### 4. PostgreSQL 18 Monitoring

```csharp
public class MonitoringController : ControllerBase
{
    private readonly IPostgreSql18MonitoringService _monitoring;

    [HttpGet("event-store-stats")]
    public async Task<EventStoreStatistics> GetEventStoreStats()
    {
        return await _monitoring.GetEventStoreStatisticsAsync();
    }

    [HttpGet("slow-queries")]
    public async Task<IEnumerable<SlowQueryInfo>> GetSlowQueries(int minDuration = 100)
    {
        return await _monitoring.GetSlowQueriesAsync(minDuration);
    }

    [HttpGet("cache-performance")]
    public async Task<CacheHitRatio> GetCachePerformance()
    {
        return await _monitoring.GetCacheHitRatioAsync();
    }

    [HttpGet("dashboard")]
    public async Task<string> GetDashboard()
    {
        return await _monitoring.GetMonitoringDashboardJsonAsync();
    }
}
```

## Testing

The implementation includes comprehensive unit tests using NUnit and FakeItEasy:

```bash
dotnet test tests/Mediso.PaymentSample.Infrastructure.Tests/
```

**Test coverage includes:**
- Constructor validation
- Service registration verification
- Activity span creation
- Logging verification
- Dispose pattern testing

## Dependencies

```xml
<PackageReference Include="Marten" Version="8.11.0" />
<PackageReference Include="Marten.AspNetCore" Version="8.11.0" />
<PackageReference Include="Npgsql" Version="9.0.2" />
<PackageReference Include="Npgsql.OpenTelemetry" Version="9.0.2" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.12.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.12.0-beta.3" />
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="9.0.5" />
```

## Database Schema

The event store automatically creates the required PostgreSQL schema:
- **Schema name:** `payment_sample`
- **Auto-creation:** Enabled for development
- **Event metadata:** Full metadata support with headers, correlation IDs

## Observability Features

- **Distributed tracing** with Jaeger/OpenTelemetry
- **Structured logging** with Serilog
- **Performance monitoring** with activity timing
- **Error tracking** with full exception context
- **Event correlation** across service boundaries

This implementation provides a production-ready event store foundation with excellent observability and debugging capabilities.