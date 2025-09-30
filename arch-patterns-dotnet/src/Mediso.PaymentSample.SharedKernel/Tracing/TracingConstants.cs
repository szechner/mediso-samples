using System.Diagnostics;

namespace Mediso.PaymentSample.SharedKernel.Tracing;

/// <summary>
/// Constants for OpenTelemetry tracing and activity sources
/// </summary>
public static class TracingConstants
{
    /// <summary>
    /// The service name used for tracing
    /// </summary>
    public const string ServiceName = "Mediso.PaymentSample";

    /// <summary>
    /// The service version for tracing
    /// </summary>
    public const string ServiceVersion = "1.0.0";
    
    /// <summary>
    /// Service names for different layers
    /// </summary>
    public const string DomainServiceName = "Mediso.PaymentSample.Domain";
    public const string ApplicationServiceName = "Mediso.PaymentSample.Application";
    public const string ApiServiceName = "Mediso.PaymentSample.Api";
    public const string InfrastructureServiceName = "Mediso.PaymentSample.Infrastructure";

    /// <summary>
    /// Activity source for domain operations
    /// </summary>
    public static readonly ActivitySource DomainActivitySource = new(
        $"{ServiceName}.Domain", 
        ServiceVersion);

    /// <summary>
    /// Activity source for application operations
    /// </summary>
    public static readonly ActivitySource ApplicationActivitySource = new(
        $"{ServiceName}.Application", 
        ServiceVersion);

    /// <summary>
    /// Activity source for API operations
    /// </summary>
    public static readonly ActivitySource ApiActivitySource = new(
        $"{ServiceName}.Api", 
        ServiceVersion);

    /// <summary>
    /// Activity source for infrastructure operations
    /// </summary>
    public static readonly ActivitySource InfrastructureActivitySource = new(
        $"{ServiceName}.Infrastructure", 
        ServiceVersion);

    // Tag names for consistent tagging across the application
    public static class Tags
    {
        public const string PaymentId = "payment.id";
        public const string PaymentState = "payment.state";
        public const string PaymentAmount = "payment.amount";
        public const string PaymentCurrency = "payment.currency";
        public const string AccountId = "account.id";
        public const string ReservationId = "reservation.id";
        public const string OperationType = "operation.type";
        public const string EventType = "event.type";
        public const string UserId = "user.id";
        public const string CorrelationId = "correlation.id";
        public const string AggregateId = "aggregate.id";
        public const string AggregateType = "aggregate.type";
        public const string AggregateVersion = "aggregate.version";
        public const string StreamId = "stream.id";
        public const string ExpectedVersion = "expected.version";
        public const string FromVersion = "from.version";
        public const string EventCount = "event.count";
        public const string Event = "event";
        public const string DatabaseOperation = "database.operation";
    }

    // Activity names for common operations
    public static class Activities
    {
        // Domain Activities
        public const string PaymentCreation = "payment.create";
        public const string PaymentStateTransition = "payment.state-transition";
        public const string AMLCheck = "payment.aml-check";
        public const string FundsReservation = "payment.funds-reservation";
        public const string PaymentJournaling = "payment.journaling";
        public const string PaymentSettlement = "payment.settlement";
        
        // Application Activities
        public const string CommandHandling = "application.command-handling";
        public const string QueryHandling = "application.query-handling";
        public const string EventHandling = "application.event-handling";
        
        // API Activities
        public const string HttpRequest = "api.http-request";
        public const string Validation = "api.validation";
        
        // Infrastructure Activities
        public const string DatabaseOperation = "infrastructure.database";
        public const string ExternalServiceCall = "infrastructure.external-service";
        public const string MessagePublishing = "infrastructure.message-publishing";
    }
}