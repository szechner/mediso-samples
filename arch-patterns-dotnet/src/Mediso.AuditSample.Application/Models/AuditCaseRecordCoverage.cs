namespace Mediso.AuditSample.Application.Models;

public sealed record AuditCaseRecordCoverage(
    AuditCaseRecord Record,
    IReadOnlyList<AuditCaseBatchLink> Links
);