using Dapper;
using Mediso.AuditSample.Domain.Services;
using Mediso.PaymentSample.SharedKernel.Audit;
using Npgsql;

namespace Mediso.AuditSample.Infrastructure.Storage;

public sealed class PgAuditRecordStore : IAuditRecordStore
{
    private readonly NpgsqlDataSource _ds;

    public PgAuditRecordStore(NpgsqlDataSource ds) => _ds = ds;

    public async Task<InsertResult> TryInsertAsync(AuditEventV1 msg, CancellationToken ct)
    {
        const string sql = @"
insert into audit_records (
  event_id, correlation_id, source, event_type, occurred_at_utc, payload_json, payload_sha256
) values (
  @EventId, @CorrelationId, @Source, @EventType, @OccurredAtUtc, cast(@PayloadJson as jsonb), @PayloadSha256
)
on conflict (event_id) do nothing;
";
        await using var conn = await _ds.OpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, msg, cancellationToken: ct));
        return new InsertResult(affected == 1);
    }

    public async Task<IReadOnlyList<AuditEventV1>> GetByCorrelationIdAsync(Guid correlationId, int take, CancellationToken ct)
    {
        const string sql = @"
select
  event_id as EventId,
  correlation_id as CorrelationId,
  source as Source,
  event_type as EventType,
  occurred_at_utc as OccurredAtUtc,
  payload_json::text as PayloadJson,
  payload_sha256 as PayloadSha256
from audit_records
where correlation_id = @correlationId
order by occurred_at_utc desc
limit @take;
";
        await using var conn = await _ds.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AuditEventV1Row>(new CommandDefinition(sql, new { correlationId, take }, cancellationToken: ct));
        return rows.Select(r => new AuditEventV1(
                r.EventId,
                r.CorrelationId,
                r.Source,
                r.EventType,
                r.OccurredAtUtc,
                r.PayloadJson,
                r.PayloadSha256
            ))
            .ToList();
    }
    
    private sealed class AuditEventV1Row
    {
        public Guid EventId { get; init; }
        public Guid CorrelationId { get; init; }
        public string Source { get; init; } = "";
        public string EventType { get; init; } = "";
        public DateTime OccurredAtUtc { get; init; }
        public string PayloadJson { get; init; } = "";
        public string PayloadSha256 { get; init; } = "";
    }
}