namespace Mediso.PaymentSample.Infrastructure.Audit;

public interface IAuditPublisher
{
    Task EmitAsync(
        string eventType,
        Guid correlationId,
        DateTimeOffset occurredAtUtc,
        object payload,
        CancellationToken ct);
}