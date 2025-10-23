-- PostgreSQL 18 Enhanced Monitoring Setup for Payment Sample
-- This script sets up monitoring extensions and optimizations

-- Enable required extensions for monitoring
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
CREATE EXTENSION IF NOT EXISTS pgstattuple;
CREATE EXTENSION IF NOT EXISTS pg_buffercache;

-- Create monitoring schema
CREATE SCHEMA IF NOT EXISTS monitoring;

-- Grant permissions to payment_user
GRANT USAGE ON SCHEMA monitoring TO payment_user;
GRANT ALL PRIVILEGES ON SCHEMA monitoring TO payment_user;

-- Create function to get event store statistics
CREATE OR REPLACE FUNCTION monitoring.get_event_store_stats()
RETURNS TABLE(
    schema_name text,
    table_name text,
    total_events bigint,
    table_size text,
    index_size text,
    total_size text
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        schemaname::text,
        tablename::text,
        n_tup_ins as total_events,
        pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as table_size,
        pg_size_pretty(pg_indexes_size(schemaname||'.'||tablename)) as index_size,
        pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename) + pg_indexes_size(schemaname||'.'||tablename)) as total_size
    FROM pg_stat_user_tables 
    WHERE schemaname = 'payment_sample' 
    AND tablename LIKE '%events%'
    ORDER BY n_tup_ins DESC;
END;
$$ LANGUAGE plpgsql;

-- Create function to get slow queries
CREATE OR REPLACE FUNCTION monitoring.get_slow_queries(min_duration_ms integer DEFAULT 100)
RETURNS TABLE(
    query_hash text,
    query text,
    calls bigint,
    total_exec_time numeric,
    mean_exec_time numeric,
    max_exec_time numeric,
    rows_returned bigint
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        md5(pss.query)::text as query_hash,
        pss.query::text,
        pss.calls,
        round(pss.total_exec_time::numeric, 2) as total_exec_time,
        round(pss.mean_exec_time::numeric, 2) as mean_exec_time,
        round(pss.max_exec_time::numeric, 2) as max_exec_time,
        pss.rows
    FROM pg_stat_statements pss
    WHERE pss.mean_exec_time > min_duration_ms
    ORDER BY pss.mean_exec_time DESC
    LIMIT 20;
END;
$$ LANGUAGE plpgsql;

-- Create function to monitor connection statistics
CREATE OR REPLACE FUNCTION monitoring.get_connection_stats()
RETURNS TABLE(
    state text,
    count bigint,
    max_duration interval
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        pa.state::text,
        count(*)::bigint,
        max(now() - pa.state_change)::interval
    FROM pg_stat_activity pa
    WHERE pa.datname = 'payment_sample'
    GROUP BY pa.state
    ORDER BY count(*) DESC;
END;
$$ LANGUAGE plpgsql;

-- Create function to get buffer cache hit ratio
CREATE OR REPLACE FUNCTION monitoring.get_cache_hit_ratio()
RETURNS TABLE(
    object_type text,
    hit_ratio numeric
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        'Buffer Cache'::text,
        round(
            (sum(heap_blks_hit) * 100.0 / nullif(sum(heap_blks_hit + heap_blks_read), 0))::numeric, 
            2
        ) as hit_ratio
    FROM pg_statio_user_tables
    UNION ALL
    SELECT 
        'Index Cache'::text,
        round(
            (sum(idx_blks_hit) * 100.0 / nullif(sum(idx_blks_hit + idx_blks_read), 0))::numeric, 
            2
        ) as hit_ratio
    FROM pg_statio_user_indexes;
END;
$$ LANGUAGE plpgsql;

-- Create view for event store monitoring dashboard
CREATE OR REPLACE VIEW monitoring.event_store_dashboard AS
SELECT 
    'Event Store Statistics' as section,
    json_agg(
        json_build_object(
            'schema', schema_name,
            'table', table_name, 
            'total_events', total_events,
            'table_size', table_size,
            'total_size', total_size
        )
    ) as data
FROM monitoring.get_event_store_stats()
UNION ALL
SELECT 
    'Cache Performance' as section,
    json_agg(
        json_build_object(
            'type', object_type,
            'hit_ratio', hit_ratio || '%'
        )
    ) as data
FROM monitoring.get_cache_hit_ratio()
UNION ALL
SELECT 
    'Connection Statistics' as section,
    json_agg(
        json_build_object(
            'state', state,
            'count', count,
            'max_duration', max_duration::text
        )
    ) as data
FROM monitoring.get_connection_stats();

-- Grant execution permissions
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA monitoring TO payment_user;
GRANT SELECT ON monitoring.event_store_dashboard TO payment_user;

-- Create indexes for better monitoring performance
CREATE INDEX IF NOT EXISTS idx_pg_stat_statements_mean_time 
ON pg_stat_statements (mean_exec_time DESC) 
WHERE mean_exec_time > 100;

-- Log successful initialization
DO $$ 
BEGIN 
    RAISE NOTICE 'PostgreSQL 18 enhanced monitoring setup completed successfully';
END $$;