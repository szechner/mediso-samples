﻿using Dapper;
using Mediso.AuditSample.Domain.Services;
using Npgsql;

namespace Mediso.AuditSample.Infrastructure.Storage;

public sealed class PgAuditCaseQueries : IPgAuditCaseQueries
{
    private readonly NpgsqlDataSource _ds;

    public PgAuditCaseQueries(NpgsqlDataSource ds) => _ds = ds;

    public async Task<IReadOnlyList<CaseJoinedRow>> GetCaseJoinedAsync(
        Guid correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        CancellationToken ct)
    {
        const string sql = @"
select
  r.id as RecordId,
  r.event_id as EventId,
  r.correlation_id as CorrelationId,
  r.source as Source,
  r.event_type as EventType,
  r.occurred_at_utc as OccurredAtUtc,
  r.payload_json::text as PayloadJson,
  r.payload_sha256 as PayloadSha256,

  i.batch_id as BatchId,
  i.leaf_index as LeafIndex,
  i.leaf_sha256 as LeafSha256,

  b.merkle_root_sha256 as MerkleRootSha256,
  
  a.tx_signature as TxSignature,
  a.chain as Chain,
  a.network as Network,
  a.verified_at_utc as VerifiedAtUtc,
  a.commitment as Commitment,
  a.slot as Slot,
  a.block_time_utc as BlockTimeUtc,
  a.anchorer_pubkey as AnchorerPubkey
from audit_records r
left join audit_batch_items i on i.audit_record_id = r.id
left join audit_batches b on b.batch_id = i.batch_id
left join audit_anchors a on a.batch_id = b.batch_id
where r.correlation_id = @correlationId
  and (@fromUtc::timestamptz is null or r.occurred_at_utc >= @fromUtc::timestamptz)
  and (@toUtc::timestamptz is null or r.occurred_at_utc <= @toUtc::timestamptz)
order by r.occurred_at_utc asc, r.id asc
limit @take::int;
";

        await using var conn = await _ds.OpenConnectionAsync(ct);

        var rows = await conn.QueryAsync<CaseJoinedRow>(
            new CommandDefinition(sql, new
            {
                correlationId,
                fromUtc,
                toUtc,
                take = take ?? 10_000
            }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<BatchLeafRow>> GetBatchLeavesAsync(Guid batchId, CancellationToken ct)
    {
        const string sql = @"
select
  leaf_index as LeafIndex,
  leaf_sha256 as LeafHex
from audit_batch_items
where batch_id = @batchId
order by leaf_index asc;
";
        await using var conn = await _ds.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<BatchLeafRow>(
            new CommandDefinition(sql, new { batchId }, cancellationToken: ct));
        return rows.AsList();
    }
}
