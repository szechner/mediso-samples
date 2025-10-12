using Microsoft.AspNetCore.Mvc;
using Wolverine;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Ports.Primary;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.SharedKernel.Tracing;
using System.Diagnostics;
using Marten;
using Mediso.PaymentSample.Application.Modules.Payments.Sagas;
using Mediso.PaymentSample.SharedKernel.Modules;
using Microsoft.Net.Http.Headers;
using Wolverine.Http;
using Wolverine.Marten;

namespace Mediso.PaymentSample.Api.Endpoints;

public static class PaymentEndpoints
{
    private readonly static string ModuleName = "Payments";

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Payments");

        payments.MapPost("/", CreatePayment)
            .WithName("CreatePayment")
            .WithSummary("Create a new payment")
            .WithOpenApi()
            .Accepts<CreatePaymentRequest>(MediaTypeHeaderValue.Parse("application/json").ToString())
            .Produces<PaymentResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

        payments.MapGet("/{id}", GetPayment)
            .WithName("GetPayment")
            .WithSummary("Get payment by ID")
            .WithOpenApi()
            .Accepts<string>(MediaTypeHeaderValue.Parse("text/plain").ToString())
            .Produces<PaymentResponse>(StatusCodes.Status200OK);

        payments.MapPost("/{id}/aml-check", MarkAMLPassed)
            .WithName("MarkAMLPassed")
            .WithSummary("Mark payment as AML passed")
            .WithOpenApi();

        payments.MapGet("/status", GetPaymentStatus)
            .WithName("GetPaymentStatus")
            .WithSummary("Get payment status by correlation ID")
            .WithOpenApi()
            .Accepts<string>(MediaTypeHeaderValue.Parse("text/plain").ToString())
            .Produces<PaymentSagaStatusResponse>(StatusCodes.Status202Accepted)
            .Produces<PaymentCompletedStatusResponse>(StatusCodes.Status200OK);
    }

    private static Task<IResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        [FromServices]
        IPaymentModule paymentModule
    )
    {
        return paymentModule.CreatePaymentAsync(request, ModuleName);
    }

    private static Task<IResult> GetPayment(
        string id,
        [FromServices]
        IMessageBus bus
    )
    {
        var query = new GetPaymentQuery()
        {
            PaymentId = PaymentId.Parse(id)
        };
        return bus.InvokeAsync<IResult>(query);
    }

    private static Task<IResult> GetPaymentStatus(
        [FromQuery]
        string? correlationId,
        [FromServices]
        IMessageBus bus
    )
    {
        var query = new GetPaymentStatusQuery()
        {
            CorrelationId = correlationId
        };
        return bus.InvokeAsync<IResult>(query);
    }


    private static async Task<IResult> MarkAMLPassed(
        string id,
        [FromBody] AMLCheckRequest request,
        [FromServices]
        ILogger<Program> logger
    )
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

            if (!Guid.TryParse(id, out _))
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
}

// Request/Response DTOs

public record AMLCheckRequest
{
    public string RuleSetVersion { get; init; } = string.Empty;
}