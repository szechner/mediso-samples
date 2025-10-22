using Microsoft.AspNetCore.Mvc;
using Wolverine;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using System.Net;
using Mediso.PaymentSample.Application.Modules.Payments;
using Mediso.PaymentSample.SharedKernel.Modules;
using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;
using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Ports;
using Microsoft.Net.Http.Headers;

namespace Mediso.PaymentSample.Api.Endpoints;

public static class PaymentEndpoints
{
    private const string PaymentsApiUri = "/api/payments";
    private const string PaymentsApiRootUri = "/";
    private const string PaymentsApiGetPaymentUri = "/{id}";
    private const string PaymentStatusApiUri = "/status";

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var payments = app.MapGroup(PaymentsApiUri).WithTags("Payments");

        payments.MapPost(PaymentsApiRootUri, CreatePayment)
            .WithName("CreatePayment")
            .WithSummary("Create a new payment")
            .WithOpenApi()
            .Accepts<CreatePaymentRequest>(MediaTypeHeaderValue.Parse("application/json").ToString())
            .Produces<PaymentResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

        payments.MapGet(PaymentsApiGetPaymentUri, GetPayment)
            .WithName("GetPayment")
            .WithSummary("Get payment by ID")
            .WithOpenApi()
            .Produces<PaymentResponse>(StatusCodes.Status200OK);

        payments.MapGet(PaymentStatusApiUri, GetPaymentStatus)
            .WithName("GetPaymentStatus")
            .WithSummary("Get payment status by correlation ID")
            .WithOpenApi()
            .Produces<PaymentSagaStatusResponse>(StatusCodes.Status202Accepted)
            .Produces<PaymentCompletedStatusResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        [FromServices]
        IPaymentModule paymentModule
    )
    {
        var paymentResult = await paymentModule.CreatePaymentAsync(request, PaymentsModule.Name);

        if (!paymentResult.IsSuccess) return Results.Problem(ModuleResult.ToProblemDetails(paymentResult));
        
        var response = new PaymentResponse
        {
            Id = "pending",
            Amount = request.Amount,
            Currency = request.Currency,
            PayerAccountId = request.PayerAccountId,
            PayeeAccountId = request.PayeeAccountId,
            Reference = request.Reference ?? string.Empty,
            State = "Initiating"
        };
        return paymentResult.HttpStatusCode is HttpStatusCode.Accepted ? Results.Accepted($"{PaymentsApiUri}{PaymentStatusApiUri}?correlationId={paymentResult.CorrelationId}", paymentResult) : Results.Ok(response);

    }

    private static async Task<IResult> GetPayment(
        Guid id,
        [FromServices]
        IPaymentModule paymentModule
    )
    {
        try
        {
            var paymentResult = await paymentModule.GetPaymentAsync(id, PaymentsModule.Name);

            if (paymentResult != null)
            {
                return Results.Ok(paymentResult);
            }
            return Results.NotFound($"Payment {id:D} not found");
        }
        catch
        {
            return Results.Problem("An error occurred while retrieving the payment");
        }
    }

    private static async Task<IResult> GetPaymentStatus(
        [FromQuery]
        string? correlationId,
        [FromServices]
        IPaymentModule paymentModule
    )
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Results.Problem("CorrelationId is required");
        }
        
        try
        {
            var paymentStatusResult = await paymentModule.GetPaymentStatusAsync(correlationId, PaymentsModule.Name);

            if (paymentStatusResult != null)
            {
                return Results.Ok(paymentStatusResult);
            }
            return Results.NotFound($"Payment with CorrelationId {correlationId:D} not found");
        }
        catch
        {
            return Results.Problem("An error occurred while retrieving the payment");
        }
    }
}