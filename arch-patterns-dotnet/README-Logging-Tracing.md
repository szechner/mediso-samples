# Logging and Distributed Tracing Setup

This document describes the comprehensive logging and distributed tracing implementation for the Payment Sample application.

## Features Implemented

### 🔍 **Structured Logging with Serilog**
- **Console Logging**: Real-time logs with structured output
- **File Logging**: Daily rotating logs with correlation IDs and trace information
- **Log Enrichment**: Automatic enrichment with environment, machine, thread, and span information
- **Correlation Tracking**: Request correlation IDs for end-to-end traceability

### 📊 **OpenTelemetry Distributed Tracing**
- **Jaeger Integration**: Complete trace export to Jaeger
- **Activity Sources**: Domain, Application, API, and Infrastructure activities
- **Custom Spans**: Detailed spans for payment operations
- **Automatic Instrumentation**: ASP.NET Core and HTTP client instrumentation
- **Context Propagation**: Trace context flows across service boundaries

### 🏗️ **Layered Implementation**
- **SharedKernel**: Core logging interfaces and tracing constants
- **Domain Layer**: Payment aggregate instrumented with tracing
- **API Layer**: Comprehensive logging with request/response tracking
- **Error Handling**: Global exception handling with structured logging

## Quick Start

### 1. Start Jaeger with Docker
```bash
# Start Jaeger all-in-one
docker-compose up -d jaeger

# Verify Jaeger is running
curl http://localhost:16686
```

### 2. Run the Payment API
```bash
# Navigate to API project
cd src/Mediso.PaymentSample.Api

# Run the application
dotnet run
```

### 3. Access Services
- **API Swagger**: http://localhost:5000/swagger
- **Jaeger UI**: http://localhost:16686
- **Log Files**: `src/Mediso.PaymentSample.Api/logs/`

## Testing the Implementation

### Create a Payment with Full Tracing
```bash
curl -X POST "http://localhost:5000/api/payments" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: test-correlation-123" \
  -d '{
    "amount": 100.50,
    "currency": "USD",
    "payerAccountId": "PAYER123",
    "payeeAccountId": "PAYEE456",
    "reference": "Test payment with tracing"
  }'
```

### Check Payment Status
```bash
curl -X GET "http://localhost:5000/api/payments/{payment-id}" \
  -H "X-Correlation-ID: test-correlation-123"
```

### Perform AML Check
```bash
curl -X POST "http://localhost:5000/api/payments/{payment-id}/aml-check" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: test-correlation-123" \
  -d '{
    "ruleSetVersion": "v2.1.0"
  }'
```

## Log Output Examples

### Structured Console Log
```
[14:32:15 INF] Creating payment for amount 100.50 USD from PAYER123 to PAYEE456 {PaymentAmount=100.5, PaymentCurrency="USD", PayerAccount="PAYER123", PayeeAccount="PAYEE456", CorrelationId="test-correlation-123", TraceId="a1b2c3d4e5f6...", SpanId="f6e5d4c3b2a1..."}
```

### Structured File Log
```json
2025-09-29 14:32:15 [INF] test-correlation-123 a1b2c3d4e5f6... f6e5d4c3b2a1... Payment 12345-abcd-6789-efgh created successfully {PaymentId="12345-abcd-6789-efgh", PaymentAmount=100.5, PaymentCurrency="USD", PayerAccount="PAYER123", PayeeAccount="PAYEE456"}
```

## Jaeger Trace Example

When you open Jaeger UI at http://localhost:16686, you'll see:

1. **Service**: `Mediso.PaymentSample`
2. **Operations**: 
   - `api.http-request` (HTTP request span)
   - `create-payment` (API endpoint span)  
   - `payment.create` (Domain operation span)
3. **Tags**: 
   - `payment.id`: Unique payment identifier
   - `payment.amount`: Payment amount
   - `payment.currency`: Payment currency
   - `correlation.id`: Request correlation ID
   - `operation.type`: Type of operation performed

## Configuration

### Logging Configuration (`appsettings.json`)
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties} {NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/payment-api-.log",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {CorrelationId} {TraceId} {SpanId} {Message:lj} {Properties} {NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### Jaeger Configuration
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": "6831",
    "Endpoint": "http://localhost:14268/api/traces"
  }
}
```

## Log Levels and Usage

### 🔴 **Error**: Exceptions and failures
```csharp
logger.LogError(exception, "Payment creation failed for {PaymentId}", paymentId);
```

### 🟠 **Warning**: Business rule violations and validation issues
```csharp  
logger.LogWarning("Invalid payment amount: {Amount}", request.Amount);
```

### 🔵 **Information**: Business operations and state changes
```csharp
logger.LogInformation("Payment {PaymentId} created successfully", paymentId);
```

### 🟢 **Debug**: Detailed diagnostic information
```csharp
logger.LogDebug("Starting operation {OperationName}", operationName);
```

## Custom Extensions Usage

### Structured Logging with Context
```csharp
var context = new LoggingContext(correlationId)
    .WithProperty("PaymentId", paymentId.ToString())
    .WithProperty("Amount", amount);
    
logger.LogWithContext(LogLevel.Information, 
    "Payment {PaymentId} processed", context, paymentId);
```

### Performance Timing
```csharp
using var timing = logger.LogTiming("PaymentCreation", correlationId);
// ... operation code ...
// Automatic logging of duration on dispose
```

### Domain Event Logging
```csharp
logger.LogDomainEvent(domainEvent, "Payment state changed");
```

## Activity Sources and Spans

### Domain Activities
- `payment.create`: Payment creation operations
- `payment.state-transition`: State changes
- `payment.aml-check`: AML compliance checks
- `payment.funds-reservation`: Fund reservation operations

### API Activities  
- `api.http-request`: HTTP request processing
- `create-payment`: Payment creation endpoints
- `get-payment`: Payment retrieval endpoints

### Custom Activity Creation
```csharp
using var activity = TracingConstants.DomainActivitySource.StartActivity("custom-operation");
activity?.SetTag("custom.property", "value");
activity?.SetTag(TracingConstants.Tags.PaymentId, paymentId.ToString());
```

## Troubleshooting

### Jaeger Not Receiving Traces
1. Check Jaeger is running: `docker ps`
2. Verify connectivity: `curl http://localhost:14268/api/traces`
3. Check configuration in `appsettings.json`

### Missing Correlation IDs
1. Ensure `X-Correlation-ID` header is sent
2. Check middleware is configured correctly
3. Verify logging context is being used

### Log Files Not Created
1. Check application has write permissions
2. Verify `logs/` directory exists
3. Check Serilog configuration in `appsettings.json`

## Performance Considerations

- **Sampling**: Configure trace sampling for production
- **Log Levels**: Use appropriate log levels to avoid noise
- **File Retention**: Set up log rotation and retention policies
- **Resource Usage**: Monitor CPU and memory impact of logging/tracing

## Production Deployment

### Environment Variables
```bash
export JAEGER__AGENTHOST=your-jaeger-host
export JAEGER__AGENTPORT=6831
export Serilog__MinimumLevel__Default=Warning
```

### Docker Compose for Production
```yaml
version: '3.8'
services:
  payment-api:
    environment:
      - Jaeger__AgentHost=jaeger
      - Serilog__MinimumLevel__Default=Information
    depends_on:
      - jaeger
      
  jaeger:
    image: jaegertracing/all-in-one:1.51
    environment:
      - COLLECTOR_OTLP_ENABLED=true
```

This implementation provides enterprise-grade observability for your payment system with complete traceability from HTTP requests through domain operations to external service calls.