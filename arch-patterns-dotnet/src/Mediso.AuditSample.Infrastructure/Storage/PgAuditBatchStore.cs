using Dapper;
using Mediso.AuditSample.Domain.Crypto;
using Mediso.AuditSample.Domain.Services;
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

        // 2) Leaf hashes + index
        var leafBytes = new List<byte[]>(rows.Count);
        var items = new List<(long RecordId, int LeafIndex, string LeafHex)>(rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];

            var leaf = Merkle.LeafFromParts(
                r.EventId,
                r.CorrelationId,
                DateTime.SpecifyKind(r.OccurredAtUtc, DateTimeKind.Utc),
                r.PayloadSha256
            );

            leafBytes.Add(leaf);
            items.Add((r.Id, i, Convert.ToHexString(leaf).ToLowerInvariant()));
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

        // 5) Insert items (leaf_index + leaf_sha256)
        await conn.ExecuteAsync(new CommandDefinition(@"
insert into audit_batch_items (batch_id, audit_record_id, leaf_index, leaf_sha256)
values (@BatchId, @RecordId, @LeafIndex, @LeafHex);
", items.Select(x => new
        {
            BatchId = batchId,
            RecordId = x.RecordId,
            LeafIndex = x.LeafIndex,
            LeafHex = x.LeafHex
        }), tx, cancellationToken: ct));

        await tx.CommitAsync(ct);

        return new CreatedBatch(
            batchId,
            rootHex,
            rows.Count,
            DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)
        );
    }

    public async Task<PendingBatch?> GetNextPendingAnchorAsync(CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);

        const string sql = @"
select batch_id as BatchId, merkle_root_sha256 as MerkleRootSha256
from audit_batches
where status = 'PendingAnchor'
  and (next_retry_at_utc is null or next_retry_at_utc <= now())
order by created_at_utc asc
limit 1;
";
        return await conn.QueryFirstOrDefaultAsync<PendingBatch>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task MarkAnchoredAsync(Guid batchId, string chain, string network, string txSignature, CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(@"
update audit_batches
set
  status = 'Anchored',
  last_error = null,
  next_retry_at_utc = null
where batch_id = @batchId;
", new { batchId }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(@"
insert into audit_anchors (batch_id, chain, network, tx_signature)
values (@batchId, @chain, @network, @txSignature)
on conflict (batch_id) do update set
  chain = excluded.chain,
  network = excluded.network,
  tx_signature = excluded.tx_signature;
", new { batchId, chain, network, txSignature }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
    }

    public async Task MarkAnchorFailedAsync(Guid batchId, string reason, CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);

        // Backoff: 5s * 2^min(retry_count, 6) => max ~320s
        await conn.ExecuteAsync(new CommandDefinition(@"
update audit_batches
set
  status = 'PendingAnchor',
  retry_count = retry_count + 1,
  last_error = @reason,
  next_retry_at_utc = now() + (interval '5 seconds' * power(2, least(retry_count, 6)))
where batch_id = @batchId;
", new { batchId, reason }, cancellationToken: ct));
    }
}
