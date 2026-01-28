namespace Mediso.AuditSample.Application.Models;

public sealed record AuditCaseVerifyResult(
    Guid CorrelationId,
    string Verdict, // VERIFIED / NOT_VERIFIED / INCONCLUSIVE
    DateTimeOffset VerifiedAtUtc,
    IReadOnlyList<string> Problems,
    int RecordsTotal,
    int RecordsAnchored
);