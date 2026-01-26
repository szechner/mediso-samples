namespace Mediso.AuditSample.Infrastructure.Storage;

public sealed record CreatedBatch(Guid BatchId, string MerkleRootSha256, int RecordsCount, DateTime FromUtc, DateTime ToUtc);

public interface IAuditBatchStore
{
    Task<CreatedBatch?> TryCreateNextBatchAsync(int maxItems, CancellationToken ct);
}