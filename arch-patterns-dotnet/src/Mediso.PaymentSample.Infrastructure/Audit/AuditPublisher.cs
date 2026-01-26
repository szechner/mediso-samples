using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mediso.PaymentSample.SharedKernel.Audit;
using Mediso.PaymentSample.SharedKernel.Crypto;
using Wolverine;

namespace Mediso.PaymentSample.Infrastructure.Audit;

public sealed class AuditPublisher : IAuditPublisher
{
    private readonly IMessageBus _bus;

    // Stabilní JSON: žádné náhodné změny casing/indent/encoder
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AuditPublisher(IMessageBus bus) => _bus = bus;

    public async Task EmitAsync(
        string eventType,
        Guid correlationId,
        DateTimeOffset occurredAtUtc,
        object payload,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("eventType is required.", nameof(eventType));

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var payloadSha256 = Hashing.Sha256Hex(payloadJson);

        var msg = new AuditEventV1(
            EventId: Guid.NewGuid(),
            CorrelationId: correlationId,
            Source: "paymentsample",
            EventType: eventType,
            OccurredAtUtc: occurredAtUtc,
            PayloadJson: payloadJson,
            PayloadSha256: payloadSha256
        );

        await _bus.PublishAsync(msg);
    }
}