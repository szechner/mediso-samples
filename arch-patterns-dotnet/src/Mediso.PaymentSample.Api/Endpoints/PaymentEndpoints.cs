using Microsoft.AspNetCore.Mvc;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.SharedKernel.Tracing;
using System.Diagnostics;

namespace Mediso.PaymentSample.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Payments");

        payments.MapPost("/", CreatePayment)
            .WithName("CreatePayment")
            .WithSummary("Create a new payment")
            .WithOpenApi();

        payments.MapGet("/{id}", GetPayment)
            .WithName("GetPayment")
            .WithSummary("Get payment by ID")
            .WithOpenApi();

        payments.MapPost("/{id}/aml-check", MarkAMLPassed)
            .WithName("MarkAMLPassed")
            .WithSummary("Mark payment as AML passed")
            .WithOpenApi();

        payments.MapPost("/{id}/reserve", ReserveFunds)
            .WithName("ReserveFunds")
            .WithSummary("Reserve funds for payment")
            .WithOpenApi();
    }

    private static async Task<IResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        IEventStore eventStore,
        ILogger<Program> logger)
    {
        using var activity = TracingConstants.ApiActivitySource.StartActivity("create-payment");
        using var timing = logger.LogTiming("CreatePayment");

        var correlationId = Activity.Current?.GetBaggageItem("CorrelationId") ?? Guid.NewGuid().ToString();
        var context = new LoggingContext(correlationId)
            .WithProperty("PaymentAmount", request.Amount)
            .WithProperty("PaymentCurrency", request.Currency)
            .WithProperty("PayerAccount", request.PayerAccountId)
            .WithProperty("PayeeAccount", request.PayeeAccountId);

        try
        {
            logger.LogWithContext(LogLevel.Information, 
                "Creating payment for amount {Amount} {Currency} from {Payer} to {Payee}", 
                context, request.Amount, request.Currency, request.PayerAccountId, request.PayeeAccountId);

            // Validate request
            if (string.IsNullOrWhiteSpace(request.PayerAccountId) || 
                string.IsNullOrWhiteSpace(request.PayeeAccountId))
            {
                logger.LogWithContext(LogLevel.Warning, "Invalid account IDs in payment creation request", context);
            return Results.BadRequest("Payer and Payee account IDs are required");
            }

            if (request.Amount <= 0)
            {
                logger.LogWithContext(LogLevel.Warning, "Invalid payment amount: {Amount}", context, request.Amount);
                return Results.BadRequest("Payment amount must be positive");
            }

            // Create payment
            var paymentId = PaymentId.New();
            var money = new Money(request.Amount, new Currency(request.Currency));
            var payerAccount = new AccountId(request.PayerAccountId);
            var payeeAccount = new AccountId(request.PayeeAccountId);

            activity?.SetTag(TracingConstants.Tags.PaymentId, paymentId.ToString());
            activity?.SetTag(TracingConstants.Tags.PaymentAmount, request.Amount.ToString());
            activity?.SetTag(TracingConstants.Tags.PaymentCurrency, request.Currency);

            var payment = Payment.Create(paymentId, money, payerAccount, payeeAccount, request.Reference);

            // Save to event store - this will generate database traces!
            await eventStore.SaveAggregateAsync(payment);

            // Log domain events
            foreach (var domainEvent in payment.UncommittedEvents)
            {
                logger.LogDomainEvent(domainEvent, "Payment domain event raised");
            }

            logger.LogWithContext(LogLevel.Information, 
                "Payment {PaymentId} created successfully", 
                context.WithProperty("PaymentId", paymentId.ToString()), paymentId);

            var response = new PaymentResponse
            {
                Id = paymentId.ToString(),
                Amount = money.Amount,
                Currency = money.Currency.Code,
                PayerAccountId = payerAccount.Value,
                PayeeAccountId = payeeAccount.Value,
                Reference = request.Reference,
                State = payment.State.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            return Results.Created($"/api/payments/{paymentId}", response);
        }
        catch (DomainException ex)
        {
            logger.LogExceptionWithContext(ex, context, "Domain validation error during payment creation");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(ex, context, "Unexpected error during payment creation");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem("An error occurred while creating the payment");
        }
    }

    private static async Task<IResult> GetPayment(
        string id,
        IEventStore eventStore,
        ILogger<Program> logger)
    {
        using var activity = TracingConstants.ApiActivitySource.StartActivity("get-payment");
        using var timing = logger.LogTiming("GetPayment", id);

        var correlationId = Activity.Current?.GetBaggageItem("CorrelationId") ?? Guid.NewGuid().ToString();
        var context = new LoggingContext(correlationId).WithProperty("PaymentId", id);

        try
        {
            logger.LogWithContext(LogLevel.Information, "Retrieving payment {PaymentId}", context, id);

            if (!Guid.TryParse(id, out var guidId))
            {
                logger.LogWithContext(LogLevel.Warning, "Invalid payment ID format: {PaymentId}", context, id);
                return Results.BadRequest("Invalid payment ID format");
            }

            activity?.SetTag(TracingConstants.Tags.PaymentId, id);
            
            // Load payment from event store - this will generate database traces!
            var paymentId = new PaymentId(guidId);
            var payment = await eventStore.LoadAggregateAsync<Payment>(paymentId.Value);
            
            if (payment == null)
            {
                logger.LogWithContext(LogLevel.Warning, "Payment {PaymentId} not found", context, id);
                return Results.NotFound($"Payment {id} not found");
            }

            var response = new PaymentResponse
            {
                Id = payment.Id.ToString(),
                Amount = payment.Amount.Amount,
                Currency = payment.Amount.Currency.Code,
                PayerAccountId = payment.PayerAccountId.Value,
                PayeeAccountId = payment.PayeeAccountId.Value,
                Reference = payment.Reference,
                State = payment.State.ToString(),
                CreatedAt = DateTimeOffset.UtcNow // You might want to get this from domain events
            };

            logger.LogWithContext(LogLevel.Information, 
                "Payment {PaymentId} retrieved successfully", context, id);

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(ex, context, "Error retrieving payment {PaymentId}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem("An error occurred while retrieving the payment");
        }
    }

    private static async Task<IResult> MarkAMLPassed(
        string id,
        [FromBody] AMLCheckRequest request,
        ILogger<Program> logger)
    {
        using var activity = TracingConstants.ApiActivitySource.StartActivity("aml-check-payment");
        using var timing = logger.LogTiming("MarkAMLPassed", id);

        var correlationId = Activity.Current?.GetBaggageItem("CorrelationId") ?? Guid.NewGuid().ToString();
        var context = new LoggingContext(correlationId)
            .WithProperty("PaymentId", id)
            .WithProperty("RuleSetVersion", request.RuleSetVersion);

        try
        {
            logger.LogWithContext(LogLevel.Information, 
                "Marking payment {PaymentId} as AML passed with ruleset {RuleSetVersion}", 
                context, id, request.RuleSetVersion);

            if (!Guid.TryParse(id, out var guidId))
            {
                logger.LogWithContext(LogLevel.Warning, "Invalid payment ID format: {PaymentId}", context, id);
                return Results.BadRequest("Invalid payment ID format");
            }

            activity?.SetTag(TracingConstants.Tags.PaymentId, id);
            activity?.SetTag("aml.ruleset_version", request.RuleSetVersion);

            // For demo purposes, simulate AML check
            await Task.Delay(100); // Simulate processing time

            logger.LogWithContext(LogLevel.Information, 
                "Payment {PaymentId} AML check completed successfully", context, id);

            return Results.Ok(new { message = "Payment marked as AML passed", paymentId = id });
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(ex, context, "Error during AML check for payment {PaymentId}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem("An error occurred during AML check");
        }
    }

    private static async Task<IResult> ReserveFunds(
        string id,
        [FromBody] ReserveFundsRequest request,
        ILogger<Program> logger)
    {
        using var activity = TracingConstants.ApiActivitySource.StartActivity("reserve-funds");
        using var timing = logger.LogTiming("ReserveFunds", id);

        var correlationId = Activity.Current?.GetBaggageItem("CorrelationId") ?? Guid.NewGuid().ToString();
        var context = new LoggingContext(correlationId)
            .WithProperty("PaymentId", id)
            .WithProperty("ReservationId", request.ReservationId);

        try
        {
            logger.LogWithContext(LogLevel.Information, 
                "Reserving funds for payment {PaymentId} with reservation {ReservationId}", 
                context, id, request.ReservationId);

            if (!Guid.TryParse(id, out var paymentGuid) || 
                !Guid.TryParse(request.ReservationId, out var reservationGuid))
            {
                logger.LogWithContext(LogLevel.Warning, "Invalid ID format", context);
                return Results.BadRequest("Invalid payment or reservation ID format");
            }

            activity?.SetTag(TracingConstants.Tags.PaymentId, id);
            activity?.SetTag(TracingConstants.Tags.ReservationId, request.ReservationId);

            // For demo purposes, simulate funds reservation
            await Task.Delay(200); // Simulate processing time

            logger.LogWithContext(LogLevel.Information, 
                "Funds reserved successfully for payment {PaymentId}", context, id);

            return Results.Ok(new { 
                message = "Funds reserved successfully", 
                paymentId = id, 
                reservationId = request.ReservationId 
            });
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(ex, context, "Error reserving funds for payment {PaymentId}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem("An error occurred while reserving funds");
        }
    }
}

// Request/Response DTOs
public record CreatePaymentRequest
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string PayerAccountId { get; init; } = string.Empty;
    public string PayeeAccountId { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

public record PaymentResponse
{
    public string Id { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string PayerAccountId { get; init; } = string.Empty;
    public string PayeeAccountId { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

public record AMLCheckRequest
{
    public string RuleSetVersion { get; init; } = string.Empty;
}

public record ReserveFundsRequest
{
    public string ReservationId { get; init; } = string.Empty;
}