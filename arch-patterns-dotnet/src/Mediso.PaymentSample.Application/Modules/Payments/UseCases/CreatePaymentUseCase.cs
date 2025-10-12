using System.Diagnostics;
using FluentValidation;
using Marten;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Sagas;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.SharedKernel.Modules;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Wolverine;
using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace Mediso.PaymentSample.Application.Modules.Payments.UseCases;

public class CreatePaymentUseCase
{
    public static async Task<IResult> Handle(
        DeliveryMessage<CreatePaymentRequest> request,
        IHttpContextAccessor http,
        IMessageBus bus,
        IQuerySession query,
        ILogger<CreatePaymentUseCase> logger
    )
    {
        return await Handle(request.Message, http, bus, query, logger);
    }

    public static async Task<IResult> Handle(
        CreatePaymentRequest request,
        IHttpContextAccessor http,
        IMessageBus bus,
        IQuerySession query,
        ILogger<CreatePaymentUseCase> logger
    )
    {
        using var activity = TracingConstants.ApiActivitySource.StartActivity("create-payment");
        using var timing = logger.LogTiming("CreatePayment");

        var correlationId = Activity.Current?.GetBaggageItem("CorrelationId") ?? Guid.NewGuid().ToString();
        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

        var context = new LoggingContext(correlationId)
            .WithProperty("PaymentAmount", request.Amount)
            .WithProperty("PaymentCurrency", request.Currency)
            .WithProperty("PayerAccount", request.PayerAccountId)
            .WithProperty("PayeeAccount", request.PayeeAccountId)
            .WithProperty("IdempotencyKey", idempotencyKey);

        try
        {
            var ctx = http.HttpContext;

            var userAgent = ctx?.Request.Headers[HeaderNames.UserAgent].ToString();
            var ip = ctx?.Connection.RemoteIpAddress?.ToString();

            logger.LogWithContext(LogLevel.Information,
                "Initiating payment saga for amount {Amount} {Currency} from {Payer} to {Payee} [CorrelationId: {CorrelationId}, IdempotencyKey: {IdempotencyKey}]",
                context, request.Amount, request.Currency, request.PayerAccountId, request.PayeeAccountId, correlationId, idempotencyKey);

            logger.LogWithContext(LogLevel.Debug,
                "Request details - Reference: {Reference}, PaymentMethod: {PaymentMethod}, UserAgent: {UserAgent}, IpAddress: {IpAddress}",
                context, request.Reference ?? string.Empty, request.PaymentMethod ?? string.Empty, userAgent ?? string.Empty, ip ?? string.Empty);

            // Validate request
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
            
            var saga = await query.Query<PaymentProcessingSaga>()
                .Where(x => x.State.IdempotencyKey == request.IdempotencyKey)
                .FirstOrDefaultAsync();

            if (saga != null)
            {
                logger.LogWithContext(LogLevel.Warning, "Payment process already exists, CorrelationId: {PaymentCorrelationId}", context, saga.State.CorrelationId);
                return Results.BadRequest($"Payment process already exists, CorrelationId: {saga.State.CorrelationId}");
            }

            // Create InitiatePaymentCommand for saga orchestration
            var customerId = new CustomerId(Guid.TryParse(request.PayerAccountId, out var customerGuid) ? customerGuid : Guid.NewGuid());
            var merchantId = new MerchantId(Guid.TryParse(request.PayeeAccountId, out var merchantGuid) ? merchantGuid : Guid.NewGuid());

            var initiateCommand = new InitiatePaymentCommand
            {
                PaymentProcessingSagaId = Guid.NewGuid(),
                CorrelationId = correlationId,
                IdempotencyKey = idempotencyKey,
                CustomerId = customerId,
                MerchantId = merchantId,
                Amount = request.Amount,
                Currency = request.Currency,
                Description = request.Reference ?? "Payment from API",
                PaymentMethod = request.PaymentMethod ?? "credit-card",
                Metadata = new Dictionary<string, string>
                {
                    { "source", "api" },
                    { "endpoint", "create-payment" },
                    { "user_agent", userAgent ?? "unknown" },
                    { "ip_address", ip ?? "unknown" }
                }
            };

            logger.LogWithContext(LogLevel.Debug,
                "Created InitiatePaymentCommand - PaymentId will be generated, Amount: {Amount}, Description: {Description}",
                context, initiateCommand.Amount, initiateCommand.Description);

            activity?.SetTag(TracingConstants.CorrelationId, correlationId);
            activity?.SetTag(TracingConstants.IdempotencyKey, idempotencyKey);
            activity?.SetTag(TracingConstants.Tags.PaymentAmount, request.Amount.ToString());
            activity?.SetTag(TracingConstants.Tags.PaymentCurrency, request.Currency);
            activity?.SetTag("saga.initiated", true);

            // Start payment workflow asynchronously by publishing the command
            logger.LogWithContext(LogLevel.Information, "Publishing async payment workflow initiation message", context);
            logger.LogWithContext(LogLevel.Information, "Command details - Type: {CommandType}, CorrelationId: {CorrelationId}, Amount: {Amount}",
                context, initiateCommand.GetType().Name, initiateCommand.CorrelationId, initiateCommand.Amount);

            try
            {
                await bus.PublishAsync(initiateCommand);
                logger.LogWithContext(LogLevel.Information,
                    "Payment workflow message published successfully [CorrelationId: {CorrelationId}]",
                    context, correlationId);
            }
            catch (Exception publishEx)
            {
                logger.LogWithContext(LogLevel.Error, "Failed to publish payment workflow initiation message: {Exception} - {ExceptionDetails}",
                    context, publishEx.Message, publishEx.ToString());
                return Results.Problem("Failed to initiate payment workflow");
            }

            // Create API response for async workflow
            var response = new PaymentResponse
            {
                Id = "pending",
                Amount = request.Amount,
                Currency = request.Currency,
                PayerAccountId = request.PayerAccountId,
                PayeeAccountId = request.PayeeAccountId,
                Reference = request.Reference ?? string.Empty,
                State = "Initiating",
                CreatedAt = DateTimeOffset.UtcNow,
                ProcessingStatus = "workflow-starting",
                CorrelationId = correlationId,
                IdempotencyKey = idempotencyKey
            };

            activity?.SetTag("payment.status", "Initiating");
            activity?.SetTag("workflow.async", true);

            return Results.Accepted($"/api/payments/status?correlationId={correlationId}", response);
        }
        catch (DomainException ex)
        {
            logger.LogExceptionWithContext(ex, context, "Domain validation error during payment saga initiation");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", "DomainValidation");
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(ex, context, "Unexpected error during payment saga initiation");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            return Results.Problem("An error occurred while initiating the payment process");
        }
    }
    
    private static async Task ValidateCommandAsync(InitiatePaymentCommand command, IValidator<InitiatePaymentCommand> validator)
    {
        var validationResult = await validator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException($"Payment initiation validation failed: {errors}");
        }
    }
}