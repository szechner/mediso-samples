namespace Mediso.AuditSample.Application.Models;

public sealed record AuditCaseRecord(
    long RecordId,
    Guid EventId,
    Guid CorrelationId,
    string Source,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string PayloadJson,
    string PayloadSha256
);