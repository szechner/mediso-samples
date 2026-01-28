namespace Mediso.AuditSample.Domain.Services;

public interface IAuditAnchorStore
{
    Task<IReadOnlyList<PendingAnchorVerifyRow>> GetPendingVerificationsAsync(int take, CancellationToken ct);
    Task MarkVerifiedAsync(Guid batchId, string commitment, long slot, DateTimeOffset? blockTimeUtc, string anchorerPubkey, CancellationToken ct);
    Task MarkVerifyFailedAsync(Guid batchId, string reason, CancellationToken ct);
}

public sealed record PendingAnchorVerifyRow(
    Guid BatchId,
    string TxSignature,
    string Chain,
    string Network,
    string MerkleRootSha256
);