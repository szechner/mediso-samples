using Dapper;
using Mediso.AuditSample.Domain.Services;
using Npgsql;

namespace Mediso.AuditSample.Infrastructure.Storage;

public sealed class PgAuditAnchorStore : IAuditAnchorStore
{
    private readonly NpgsqlDataSource _ds;

    public PgAuditAnchorStore(NpgsqlDataSource ds) => _ds = ds;

    public async Task<IReadOnlyList<PendingAnchorVerifyRow>> GetPendingVerificationsAsync(int take, CancellationToken ct)
    {
        const string sql = @"
select
  a.batch_id as BatchId,
  a.tx_signature as TxSignature,
  a.chain as Chain,
  a.network as Network,
  b.merkle_root_sha256 as MerkleRootSha256
from audit_anchors a
join audit_batches b on b.batch_id = a.batch_id
where a.verified_at_utc is null
order by a.anchored_at_utc asc
limit @take::int;
";
        await using var conn = await _ds.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PendingAnchorVerifyRow>(new CommandDefinition(sql, new { take }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task MarkVerifiedAsync(Guid batchId, string commitment, long slot, DateTimeOffset? blockTimeUtc, string anchorerPubkey, CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(@"
update audit_anchors
set
  commitment = @commitment,
  slot = @slot,
  block_time_utc = @blockTimeUtc,
  anchorer_pubkey = @anchorerPubkey,
  verified_at_utc = now()
where batch_id = @batchId;
", new
        {
            batchId,
            commitment,
            slot,
            blockTimeUtc = blockTimeUtc?.UtcDateTime,
            anchorerPubkey
        }, cancellationToken: ct));
    }

    public async Task MarkVerifyFailedAsync(Guid batchId, string reason, CancellationToken ct)
    {
        // Pro demo to jen logujeme do audit_batches.last_error (nechceme měnit audit_anchors schema o error sloupce)
        await using var conn = await _ds.OpenConnectionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(@"
update audit_batches
set
  last_error = @reason
where batch_id = @batchId;
", new { batchId, reason }, cancellationToken: ct));
    }
}


