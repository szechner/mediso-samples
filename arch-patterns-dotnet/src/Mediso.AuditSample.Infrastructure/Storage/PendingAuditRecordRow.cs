namespace Mediso.AuditSample.Infrastructure.Storage;

internal sealed class PendingAuditRecordRow
{
    public long Id { get; init; }
    public Guid EventId { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string PayloadSha256 { get; init; } = "";
}