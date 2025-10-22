using System.Diagnostics;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Abstractions;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.SharedKernel.Modules.ModuleFacades.Contracts;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Application.Modules.Payments.UseCases;

public class GetPaymentUseCase
{
    public static async Task<PaymentResponse?> Handle(GetPaymentQuery query,
        IEventStore eventStore,
        ILogger<GetPaymentUseCase> logger)
    {
        using var activity = TracingConstants.ApiActivitySource.StartActivity("get-payment");
        using var timing = logger.LogTiming("GetPayment", query.PaymentId.ToString());

        var correlationId = Activity.Current?.GetBaggageItem("CorrelationId") ?? Guid.NewGuid().ToString();
        var context = new LoggingContext(correlationId).WithProperty("PaymentId", query.PaymentId.ToString());

        try
        {
            logger.LogWithContext(LogLevel.Information, "Retrieving payment {PaymentId}", context, query.PaymentId.ToString());

            activity?.SetTag(TracingConstants.Tags.PaymentId, query.PaymentId.ToString());

            // Load payment from event store - this will generate database traces!
            var payment = await eventStore.LoadAggregateAsync<Payment>(query.PaymentId);

            if (payment == null)
            {
                logger.LogWithContext(LogLevel.Warning, "Payment {PaymentId} not found", context, query.PaymentId.ToString());
                return null;
            }

            var response = new PaymentResponse
            {
                Id = payment.Id.ToString(),
                Amount = payment.Amount.Amount,
                Currency = payment.Amount.Currency.Code,
                PayerAccountId = payment.PayerAccountId.Value,
                PayeeAccountId = payment.PayeeAccountId.Value,
                Reference = payment.Reference,
                State = payment.State.ToString("G"),
                RequestedAt = payment.RequestedAt,
                SettledAt = payment.SettledAt,
                DeclinedReason = payment.DeclinedReason,
            };
            
            logger.LogWithContext(LogLevel.Information,
                "Payment {PaymentId} retrieved successfully", context, query.PaymentId.ToString());

            return response;
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(ex, context, "Error retrieving payment {PaymentId}", query.PaymentId.ToString());
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}