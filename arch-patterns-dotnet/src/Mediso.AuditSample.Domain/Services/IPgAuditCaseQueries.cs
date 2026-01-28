namespace Mediso.AuditSample.Domain.Services;

public interface IPgAuditCaseQueries
{
    Task<IReadOnlyList<CaseJoinedRow>> GetCaseJoinedAsync(
        Guid correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        CancellationToken ct);

    Task<IReadOnlyList<BatchLeafRow>> GetBatchLeavesAsync(Guid batchId, CancellationToken ct);
}

public sealed class CaseJoinedRow
{
    public long RecordId { get; init; }
    public Guid EventId { get; init; }
    public Guid CorrelationId { get; init; }
    public string Source { get; init; } = "";
    public string EventType { get; init; } = "";
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string PayloadJson { get; init; } = "";
    public string PayloadSha256 { get; init; } = "";

    public Guid? BatchId { get; init; }
    public int? LeafIndex { get; init; }
    public string? LeafSha256 { get; init; }

    public string? MerkleRootSha256 { get; init; }

    public string? TxSignature { get; init; }
    public string? Chain { get; init; }
    public string? Network { get; init; }

    // NEW: populated by verify worker (audit_anchors)
    public DateTimeOffset? VerifiedAtUtc { get; init; }
    public string? Commitment { get; init; }
    public long? Slot { get; init; }
    public DateTimeOffset? BlockTimeUtc { get; init; }
    public string? AnchorerPubkey { get; init; }
}

public sealed class BatchLeafRow
{
    public int LeafIndex { get; init; }
    public string LeafHex { get; init; } = "";
}