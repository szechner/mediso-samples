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
create index if not exists ix_audit_records_id on audit_records(id);

create table if not exists audit_batches (
  batch_id uuid primary key,
  created_at_utc timestamptz not null,
  from_utc timestamptz not null,
  to_utc timestamptz not null,
  records_count int not null,
  merkle_root_sha256 char(64) not null unique,
  status text not null,

  retry_count int not null default 0,
  next_retry_at_utc timestamptz null,
  last_error text null
);

create table if not exists audit_batch_items (
  batch_id uuid not null references audit_batches(batch_id) on delete cascade,
  audit_record_id bigint not null references audit_records(id) on delete cascade,

  leaf_index int null, -- backfill v další části
  leaf_sha256 char(64) not null,

  primary key (batch_id, audit_record_id)
);

create index if not exists ix_audit_batch_items_batch on audit_batch_items(batch_id);
create index if not exists ix_audit_batch_items_record on audit_batch_items(audit_record_id);
create index if not exists ix_audit_batch_items_batch_leaf on audit_batch_items(batch_id, leaf_index);

create table if not exists audit_anchors (
  batch_id uuid primary key references audit_batches(batch_id) on delete cascade,
  chain text not null,
  network text not null,
  tx_signature text not null unique,
  anchored_at_utc timestamptz not null default now(),

  commitment text null,
  slot bigint null,
  block_time_utc timestamptz null,
  anchorer_pubkey text null,
  verified_at_utc timestamptz null
);

create index if not exists ix_audit_batches_status_created on audit_batches(status, created_at_utc);
create index if not exists ix_audit_records_corr_occurred on audit_records(correlation_id, occurred_at_utc);

with ranked as (
  select
    batch_id,
    audit_record_id,
    row_number() over(partition by batch_id order by audit_record_id asc) - 1 as rn
  from audit_batch_items
)
update audit_batch_items i
set leaf_index = r.rn
from ranked r
where i.batch_id = r.batch_id
  and i.audit_record_id = r.audit_record_id
  and i.leaf_index is null;

alter table audit_batch_items
  alter column leaf_index set not null;
";
        await using var conn = await ds.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}
