namespace Mediso.AuditSample.Application.Models;

public sealed record AuditCaseExportManifest(
    Guid CorrelationId,
    DateTimeOffset ExportedAtUtc,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int RecordsTotal,
    int RecordsAnchored,
    string Verdict
);