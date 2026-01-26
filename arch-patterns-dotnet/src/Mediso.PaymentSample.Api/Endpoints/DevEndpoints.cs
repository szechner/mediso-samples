using Mediso.PaymentSample.SharedKernel.Audit;
using Mediso.PaymentSample.SharedKernel.Crypto;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Mediso.PaymentSample.Api.Endpoints;

public static class DevEndpoints
{
    private const string DevApiUri = "/api/dev";
    private const string AuditUri = "/audit";
    private const string AuditPingUri= "/ping";
    
    public static void MapDevEndpoints(this WebApplication app)
    {
        var dev = app.MapGroup(DevApiUri).WithTags("Dev");

        dev.MapPost(AuditUri + AuditPingUri, AuditPing)
            .WithName("AuditPing")
            .WithSummary("Ping the audit service")
            .WithOpenApi();
    }
    
    private static async Task <IResult> AuditPing(
        [FromServices]
        IMessageBus  bus
    )
    {
        var payloadJson = """{"hello":"kafka"}""";

        var msg = new AuditEventV1(
            EventId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            Source: "paymentsample",
            EventType: "DevPing",
            OccurredAtUtc: DateTimeOffset.UtcNow,
            PayloadJson: payloadJson,
            PayloadSha256: Hashing.Sha256Hex(payloadJson)
        );

        await bus.PublishAsync(msg);
        return Results.Ok(new { msg.EventId, Topic = "payments.audit.v1" });
    }
}