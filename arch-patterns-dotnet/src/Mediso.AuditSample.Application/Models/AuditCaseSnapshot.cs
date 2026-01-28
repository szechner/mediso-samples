namespace Mediso.AuditSample.Application.Models;

public sealed record AuditCaseSnapshot(
    Guid CorrelationId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    IReadOnlyList<AuditCaseRecordCoverage> Records
);