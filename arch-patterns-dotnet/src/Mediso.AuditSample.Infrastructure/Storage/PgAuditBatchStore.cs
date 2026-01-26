using Dapper;
using Mediso.AuditSample.Infrastructure.Crypto;
using Npgsql;

namespace Mediso.AuditSample.Infrastructure.Storage;

public sealed class PgAuditBatchStore : IAuditBatchStore
{
    private readonly NpgsqlDataSource _ds;

    public PgAuditBatchStore(NpgsqlDataSource ds) => _ds = ds;

    public async Task<CreatedBatch?> TryCreateNextBatchAsync(int maxItems, CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 1) Vyber "nezařazené" recordy (ty, co nejsou v audit_batch_items)
        var rows = (await conn.QueryAsync<PendingAuditRecordRow>(new CommandDefinition(@"
select r.id as Id, r.event_id as EventId, r.correlation_id as CorrelationId, r.occurred_at_utc as OccurredAtUtc, r.payload_sha256 as PayloadSha256
from audit_records r
left join audit_batch_items i on i.audit_record_id = r.id
where i.audit_record_id is null
order by r.id asc
limit @take;
", new { take = maxItems }, tx, cancellationToken: ct))).AsList();

        if (rows.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        // 2) Leaf hashes
        var leafBytes = new List<byte[]>(rows.Count);
        var leafHexByRecordId = new List<(long RecordId, string LeafHex)>(rows.Count);

        foreach (var r in rows)
        {
            var leaf = Merkle.LeafFromParts(r.EventId, r.CorrelationId, r.OccurredAtUtc, r.PayloadSha256);
            leafBytes.Add(leaf);
            leafHexByRecordId.Add((r.Id, Convert.ToHexString(leaf).ToLowerInvariant()));
        }

        var rootHex = Merkle.ComputeRootHex(leafBytes);

        // 3) Batch metadata
        var batchId = Guid.NewGuid();
        var fromUtc = rows.Min(x => x.OccurredAtUtc);
        var toUtc = rows.Max(x => x.OccurredAtUtc);

        // 4) Insert batch
        await conn.ExecuteAsync(new CommandDefinition(@"
insert into audit_batches (
  batch_id, created_at_utc, from_utc, to_utc, records_count, merkle_root_sha256, status
) values (
  @BatchId, now(), @FromUtc, @ToUtc, @Count, @Root, @Status
);
", new
        {
            BatchId = batchId,
            FromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
            ToUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc),
            Count = rows.Count,
            Root = rootHex,
            Status = "PendingAnchor"
        }, tx, cancellationToken: ct));

        // 5) Insert items
        await conn.ExecuteAsync(new CommandDefinition(@"
insert into audit_batch_items (batch_id, audit_record_id, leaf_sha256)
values (@BatchId, @RecordId, @LeafHex);
", leafHexByRecordId.Select(x => new { BatchId = batchId, RecordId = x.RecordId, LeafHex = x.LeafHex }), tx, cancellationToken: ct));

        await tx.CommitAsync(ct);

        return new CreatedBatch(batchId, rootHex, rows.Count,
            DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(toUtc, DateTimeKind.Utc));
    }
}
