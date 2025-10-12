using System.Diagnostics;
using Marten;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;
using Mediso.PaymentSample.Application.Modules.Payments.Sagas;
using Mediso.PaymentSample.SharedKernel.Logging;
using Mediso.PaymentSample.SharedKernel.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Mediso.PaymentSample.Application.Modules.Payments.UseCases;

public class GetPaymentStatusUseCase
{
    public static async Task<IResult> Handle(GetPaymentStatusQuery query, IQuerySession session,
    [FromServices]
    ILogger<GetPaymentStatusUseCase> logger)
    {
        using var activity = TracingConstants.ApiActivitySource.StartActivity("get-payment-status");
        using var timing = logger.LogTiming("GetPaymentStatus");

        var requestCorrelationId = query.CorrelationId ?? Activity.Current?.GetBaggageItem("CorrelationId") ?? Guid.NewGuid().ToString();
        var context = new LoggingContext(requestCorrelationId).WithProperty("CorrelationId", query.CorrelationId ?? "none");

        try
        {
            if (string.IsNullOrWhiteSpace(query.CorrelationId))
            {
                logger.LogWithContext(LogLevel.Warning, "Missing correlation ID in status request", context);
                return Results.BadRequest("Correlation ID is required");
            }

            logger.LogWithContext(LogLevel.Information, "Retrieving payment status by correlation ID {CorrelationId}", context, query.CorrelationId);

            activity?.SetTag(TracingConstants.CorrelationId, query.CorrelationId);

            var saga = await session.Query<PaymentProcessingSaga>()
                .Where(x => x.State.CorrelationId == query.CorrelationId)
                .SingleOrDefaultAsync();

            if (saga is null)
            {
                var payments = await FindStreamsByCorrelationAsync(query.CorrelationId, session);

                if (payments.Count > 0)
                {
                    logger.LogWithContext(LogLevel.Information,
                        "No active saga found, but payment(s) exist for correlation ID {CorrelationId}",
                        context, query.CorrelationId);

                    return Results.Ok(new PaymentCompletedStatusResponse
                    {
                        CorrelationId = query.CorrelationId,
                        Status = PaymentSagaStatus.Completed.ToString(),
                        PaymentIds = payments.ToArray()
                    });
                }

                return Results.NotFound(new { query.CorrelationId, status = "Unknown", message = "Payment not found" });
            }

            var step = saga.State.CurrentStep.ToString();
            var status = saga.State.Status.ToString();

            var response = new PaymentSagaStatusResponse
            {
                CorrelationId = query.CorrelationId,
                PaymentId = saga.State.PaymentId?.Value.ToString(),
                CurrentStep = step,
                Status = status,
                StartedAt = saga.State.StartedAt,
                UpdatedAt = saga.State.CompletedAt ?? saga.State.StartedAt,
                ErrorMessage = saga.State.FailureReason
            };

            logger.LogWithContext(LogLevel.Information,
                "Payment status retrieved for correlation ID {CorrelationId}", context, query.CorrelationId);

            return Results.Accepted($"/api/payments/status?correlationId={query.CorrelationId}",response);
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(ex, context, "Error retrieving payment status for correlation ID {CorrelationId}", query.CorrelationId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem("An error occurred while retrieving payment status");
        }
    }
    
    private static async Task<IReadOnlyList<Guid>> FindStreamsByCorrelationAsync(
        string correlationId,
        IQuerySession query
    )
    {
        var streamIds = await query.Events.QueryAllRawEvents()
            .Where(e => e.CorrelationId == correlationId)
            .Select(e => e.StreamId)
            .Distinct()
            .ToListAsync();

        return streamIds;
    }
}