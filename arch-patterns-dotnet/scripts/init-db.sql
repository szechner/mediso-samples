-- Payment Sample Database Initialization Script
-- This script sets up the initial database structure for the Payment Sample application

-- Create extensions needed by Marten
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create application user (if not already created by environment)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'payment_user') THEN
        CREATE ROLE payment_user WITH LOGIN PASSWORD 'payment_password';
    END IF;
END$$;

-- Grant necessary permissions
GRANT ALL PRIVILEGES ON DATABASE payment_sample TO payment_user;
GRANT ALL ON SCHEMA public TO payment_user;
GRANT ALL ON ALL TABLES IN SCHEMA public TO payment_user;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO payment_user;

-- Set default privileges for future objects
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO payment_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO payment_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON FUNCTIONS TO payment_user;

-- Create a simple health check table
CREATE TABLE IF NOT EXISTS health_check (
    id SERIAL PRIMARY KEY,
    check_time TIMESTAMP DEFAULT NOW(),
    status TEXT DEFAULT 'healthy'
);

INSERT INTO health_check (status) VALUES ('Database initialized successfully');

-- Note: Marten will create its own tables and schema automatically
-- This includes:
-- - mt_events (event stream)
-- - mt_streams (aggregate streams)  
-- - mt_doc_* tables for document storage
-- - Various indexes and functions for projections