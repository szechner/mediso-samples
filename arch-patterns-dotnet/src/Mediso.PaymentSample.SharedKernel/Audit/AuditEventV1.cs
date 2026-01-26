namespace Mediso.PaymentSample.SharedKernel.Audit;

public sealed record AuditEventV1(
    Guid EventId,
    Guid CorrelationId,
    string Source,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string PayloadJson,
    string PayloadSha256
);