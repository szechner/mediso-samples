namespace Mediso.AuditSample.Infrastructure.Storage;

public sealed record CreatedBatch(Guid BatchId, string MerkleRootSha256, int RecordsCount, DateTime FromUtc, DateTime ToUtc);
public sealed record PendingBatch(Guid BatchId, string MerkleRootSha256);

public interface IAuditBatchStore
{
    Task<CreatedBatch?> TryCreateNextBatchAsync(int maxItems, CancellationToken ct);
    
    
    Task<PendingBatch?> GetNextPendingAnchorAsync(CancellationToken ct);
    Task MarkAnchoredAsync(Guid batchId, string chain, string network, string txSignature, CancellationToken ct);
    Task MarkAnchorFailedAsync(Guid batchId, string reason, CancellationToken ct);
}