using Dapper;
using Npgsql;

namespace Mediso.AuditSample.Infrastructure.Storage;

public static class AuditSchema
{
    public static async Task EnsureAsync(NpgsqlDataSource ds, CancellationToken ct)
    {
        const string sql = @"
create schema if not exists audit_sample;

create table if not exists audit_records (
  id bigserial primary key,
  event_id uuid not null unique,
  correlation_id uuid not null,
  source text not null,
  event_type text not null,
  occurred_at_utc timestamptz not null,
  payload_json jsonb not null,
  payload_sha256 char(64) not null,
  ingested_at_utc timestamptz not null default now()
);

create index if not exists ix_audit_records_correlation on audit_records(correlation_id);
create index if not exists ix_audit_records_event_type on audit_records(event_type);
create index if not exists ix_audit_records_occurred on audit_records(occurred_at_utc);

create table if not exists audit_batches (
  batch_id uuid primary key,
  created_at_utc timestamptz not null,
  from_utc timestamptz not null,
  to_utc timestamptz not null,
  records_count int not null,
  merkle_root_sha256 char(64) not null unique,
  status text not null
);

create table if not exists audit_batch_items (
  batch_id uuid not null references audit_batches(batch_id) on delete cascade,
  audit_record_id bigint not null references audit_records(id) on delete cascade,
  leaf_sha256 char(64) not null,
  primary key (batch_id, audit_record_id)
);

create index if not exists ix_audit_batch_items_batch on audit_batch_items(batch_id);
create index if not exists ix_audit_batch_items_record on audit_batch_items(audit_record_id);

create index if not exists ix_audit_records_id on audit_records(id);

";
        await using var conn = await ds.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}