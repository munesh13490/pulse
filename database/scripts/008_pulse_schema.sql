CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS pulse;
SET LOCAL search_path TO pulse, public;

CREATE OR REPLACE FUNCTION pulse.set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN NEW.updated_at = now(); RETURN NEW; END $$;

CREATE TABLE IF NOT EXISTS tenants (
 tenant_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 name varchar(200) NOT NULL,
 slug varchar(100) NOT NULL UNIQUE,
 is_active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS tenant_rate_limits (
 tenant_id uuid PRIMARY KEY REFERENCES tenants(tenant_id) ON DELETE CASCADE,
 requests_per_minute integer NOT NULL DEFAULT 100 CHECK (requests_per_minute > 0),
 events_per_minute integer NOT NULL DEFAULT 10000 CHECK (events_per_minute > 0),
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS roles (
 role_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 role_code varchar(50) NOT NULL UNIQUE,
 role_name varchar(100) NOT NULL,
 description text,
 is_system_role boolean NOT NULL DEFAULT true,
 is_active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS app_users (
 user_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid REFERENCES tenants(tenant_id) ON DELETE CASCADE,
 email varchar(320) NOT NULL,
 normalized_email varchar(320) NOT NULL,
 password_hash text NOT NULL,
 first_name varchar(100),
 last_name varchar(100),
 role_id uuid NOT NULL REFERENCES roles(role_id),
 is_active boolean NOT NULL DEFAULT true,
 failed_login_count integer NOT NULL DEFAULT 0 CHECK (failed_login_count >= 0),
 lockout_until timestamptz,
 security_stamp uuid NOT NULL DEFAULT gen_random_uuid(),
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_tenant_email ON app_users(tenant_id, normalized_email) WHERE tenant_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_platform_email ON app_users(normalized_email) WHERE tenant_id IS NULL;
CREATE TABLE IF NOT EXISTS devices (
 device_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL REFERENCES tenants(tenant_id) ON DELETE CASCADE,
 external_id varchar(120) NOT NULL,
 name varchar(200) NOT NULL,
 description text,
 status varchar(20) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DISABLED')),
 last_seen_at timestamptz,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(tenant_id, external_id), UNIQUE(tenant_id, device_id)
);
CREATE TABLE IF NOT EXISTS device_api_keys (
 key_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL,
 device_id uuid NOT NULL,
 key_digest bytea NOT NULL,
 digest_version smallint NOT NULL DEFAULT 1,
 name varchar(120),
 expires_at timestamptz,
 revoked_at timestamptz,
 last_used_at timestamptz,
 created_at timestamptz NOT NULL DEFAULT now(),
 FOREIGN KEY(tenant_id, device_id) REFERENCES devices(tenant_id, device_id) ON DELETE CASCADE,
 UNIQUE(key_digest)
);
CREATE INDEX IF NOT EXISTS ix_device_api_keys_device ON device_api_keys(tenant_id, device_id) WHERE revoked_at IS NULL;

-- Idempotency ledger enforces uniqueness across concurrent requests and all telemetry dates.
CREATE TABLE IF NOT EXISTS telemetry_idempotency (
 tenant_id uuid NOT NULL REFERENCES tenants(tenant_id) ON DELETE CASCADE,
 device_id uuid NOT NULL,
 event_id uuid NOT NULL,
 received_at timestamptz NOT NULL DEFAULT now(),
 expires_at timestamptz NOT NULL DEFAULT (now() + interval '365 days'),
 PRIMARY KEY (tenant_id, device_id, event_id),
 FOREIGN KEY(tenant_id, device_id) REFERENCES devices(tenant_id, device_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_telemetry_idempotency_expiry ON telemetry_idempotency(expires_at);
CREATE TABLE IF NOT EXISTS telemetry_events (
 telemetry_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL,
 device_id uuid NOT NULL,
 event_id uuid NOT NULL,
 metric_name varchar(120) NOT NULL,
 metric_value double precision NOT NULL CHECK (metric_value NOT IN ('Infinity'::float8,'-Infinity'::float8,'NaN'::float8)),
 event_timestamp timestamptz NOT NULL,
 received_at timestamptz NOT NULL DEFAULT now(),
 metadata jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object'),
 FOREIGN KEY(tenant_id, device_id, event_id) REFERENCES telemetry_idempotency(tenant_id, device_id, event_id) ON DELETE CASCADE,
 UNIQUE(tenant_id, device_id, event_id)
);
CREATE INDEX IF NOT EXISTS ix_telemetry_events_chart ON telemetry_events(tenant_id, device_id, metric_name, event_timestamp DESC);
CREATE INDEX IF NOT EXISTS ix_telemetry_events_retention ON telemetry_events(event_timestamp);

CREATE TABLE IF NOT EXISTS alert_rules (
 rule_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL REFERENCES tenants(tenant_id) ON DELETE CASCADE,
 device_id uuid,
 name varchar(200) NOT NULL,
 metric_name varchar(120) NOT NULL,
 rule_type varchar(20) NOT NULL CHECK (rule_type IN ('THRESHOLD','RATE_OF_CHANGE')),
 is_enabled boolean NOT NULL DEFAULT true,
 current_version integer NOT NULL DEFAULT 1 CHECK (current_version > 0),
 created_by uuid,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(tenant_id, rule_id),
 FOREIGN KEY(tenant_id, device_id) REFERENCES devices(tenant_id, device_id),
 FOREIGN KEY(created_by) REFERENCES app_users(user_id)
);
CREATE TABLE IF NOT EXISTS rule_versions (
 rule_version_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL,
 rule_id uuid NOT NULL,
 version_number integer NOT NULL CHECK (version_number > 0),
 comparison_operator varchar(3) NOT NULL CHECK (comparison_operator IN ('>','>=','<','<=')),
 threshold_value double precision NOT NULL CHECK (threshold_value NOT IN ('Infinity'::float8,'-Infinity'::float8,'NaN'::float8)),
 window_seconds integer CHECK (window_seconds > 0),
 configuration jsonb NOT NULL DEFAULT '{}'::jsonb,
 created_by uuid REFERENCES app_users(user_id),
 created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(rule_id, version_number), UNIQUE(tenant_id, rule_version_id),
 FOREIGN KEY(tenant_id, rule_id) REFERENCES alert_rules(tenant_id, rule_id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS suppression_policies (
 suppression_policy_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL,
 rule_id uuid NOT NULL,
 suppress_for_seconds integer NOT NULL DEFAULT 300 CHECK (suppress_for_seconds >= 0),
 is_enabled boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(tenant_id, rule_id),
 FOREIGN KEY(tenant_id, rule_id) REFERENCES alert_rules(tenant_id, rule_id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS alerts (
 alert_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL REFERENCES tenants(tenant_id) ON DELETE CASCADE,
 device_id uuid NOT NULL,
 rule_version_id uuid NOT NULL,
 source_event_id uuid,
 severity varchar(20) NOT NULL DEFAULT 'WARNING' CHECK (severity IN ('INFO','WARNING','CRITICAL')),
 status varchar(20) NOT NULL DEFAULT 'OPEN' CHECK (status IN ('OPEN','ACKNOWLEDGED','RESOLVED')),
 message text NOT NULL,
 trigger_value double precision,
 triggered_at timestamptz NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 acknowledged_by uuid REFERENCES app_users(user_id),
 acknowledged_at timestamptz,
 resolved_by uuid REFERENCES app_users(user_id),
 resolved_at timestamptz,
 row_version integer NOT NULL DEFAULT 1 CHECK (row_version > 0),
 FOREIGN KEY(tenant_id, device_id) REFERENCES devices(tenant_id, device_id),
 FOREIGN KEY(tenant_id, rule_version_id) REFERENCES rule_versions(tenant_id, rule_version_id),
 FOREIGN KEY(tenant_id, device_id, source_event_id) REFERENCES telemetry_events(tenant_id, device_id, event_id) ON DELETE SET NULL (source_event_id),
 UNIQUE(tenant_id, alert_id)
);
CREATE INDEX IF NOT EXISTS ix_alerts_page ON alerts(tenant_id, created_at DESC, alert_id DESC);
CREATE INDEX IF NOT EXISTS ix_alerts_filters ON alerts(tenant_id, status, severity, created_at DESC);
CREATE TABLE IF NOT EXISTS alert_transitions (
 transition_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL,
 alert_id uuid NOT NULL,
 from_status varchar(20) NOT NULL CHECK (from_status IN ('OPEN','ACKNOWLEDGED','RESOLVED')),
 to_status varchar(20) NOT NULL CHECK (to_status IN ('ACKNOWLEDGED','RESOLVED')),
 actor_user_id uuid REFERENCES app_users(user_id),
 comment text,
 occurred_at timestamptz NOT NULL DEFAULT now(),
 FOREIGN KEY(tenant_id, alert_id) REFERENCES alerts(tenant_id, alert_id) ON DELETE CASCADE,
 CONSTRAINT valid_alert_transition CHECK ((from_status = 'OPEN' AND to_status IN ('ACKNOWLEDGED','RESOLVED')) OR (from_status = 'ACKNOWLEDGED' AND to_status = 'RESOLVED'))
);
CREATE INDEX IF NOT EXISTS ix_alert_transitions_alert ON alert_transitions(tenant_id, alert_id, occurred_at);
CREATE TABLE IF NOT EXISTS hourly_rollups (
 rollup_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL,
 device_id uuid NOT NULL,
 metric_name varchar(120) NOT NULL,
 hour_start timestamptz NOT NULL,
 sample_count bigint NOT NULL CHECK (sample_count >= 0),
 min_value double precision NOT NULL,
 max_value double precision NOT NULL,
 sum_value double precision NOT NULL,
 avg_value double precision NOT NULL,
 calculated_at timestamptz NOT NULL DEFAULT now(),
 FOREIGN KEY(tenant_id, device_id) REFERENCES devices(tenant_id, device_id) ON DELETE CASCADE,
 UNIQUE(tenant_id, device_id, metric_name, hour_start)
);
CREATE INDEX IF NOT EXISTS ix_hourly_rollups_chart ON hourly_rollups(tenant_id, device_id, metric_name, hour_start DESC);
CREATE TABLE IF NOT EXISTS outbox_messages (
 outbox_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tenant_id uuid NOT NULL REFERENCES tenants(tenant_id) ON DELETE CASCADE,
 event_type varchar(120) NOT NULL,
 aggregate_id uuid,
 payload jsonb NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(),
 available_at timestamptz NOT NULL DEFAULT now(),
 processed_at timestamptz,
 attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
 last_error text
);
CREATE INDEX IF NOT EXISTS ix_outbox_pending ON outbox_messages(available_at, created_at) WHERE processed_at IS NULL;

-- Worker lease prevents multiple replicas from processing the same scheduled job concurrently.
CREATE TABLE IF NOT EXISTS worker_leases (
 lease_name varchar(120) PRIMARY KEY,
 owner_id varchar(200) NOT NULL,
 leased_until timestamptz NOT NULL,
 updated_at timestamptz NOT NULL DEFAULT now()
);

-- updated_at maintenance
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY['tenants','tenant_rate_limits','roles','app_users','devices','alert_rules','suppression_policies','alerts'] LOOP
  EXECUTE format('DROP TRIGGER IF EXISTS trg_updated_at ON pulse.%I', t);
  EXECUTE format('CREATE TRIGGER trg_updated_at BEFORE UPDATE ON pulse.%I FOR EACH ROW EXECUTE FUNCTION pulse.set_updated_at()', t);
 END LOOP;
END $$;

-- Protect immutable rule versions and transition audit entries against changes by app role.
CREATE OR REPLACE FUNCTION pulse.reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Historical records are immutable'; END $$;
DROP TRIGGER IF EXISTS trg_immutable_rule_versions ON rule_versions;
CREATE TRIGGER trg_immutable_rule_versions BEFORE UPDATE OR DELETE ON rule_versions FOR EACH ROW EXECUTE FUNCTION pulse.reject_mutation();
DROP TRIGGER IF EXISTS trg_immutable_alert_transitions ON alert_transitions;
CREATE TRIGGER trg_immutable_alert_transitions BEFORE UPDATE OR DELETE ON alert_transitions FOR EACH ROW EXECUTE FUNCTION pulse.reject_mutation();

-- Example tenant bootstrap data: creates tenant and its quota; no default passwords.
-- The bootstrap must run as the migration/owner role, not the ordinary application role.
INSERT INTO tenants(name,slug) VALUES ('Demo Tenant','demo-tenant') ON CONFLICT(slug) DO NOTHING;
INSERT INTO tenant_rate_limits(tenant_id)
 SELECT tenant_id FROM tenants WHERE slug='demo-tenant'
 ON CONFLICT(tenant_id) DO NOTHING;

-- RLS: app sets tenant ID with SELECT set_config('app.tenant_id', '<uuid>', true)
-- inside each transaction. No tenant setting => no access.
CREATE OR REPLACE FUNCTION pulse.current_tenant_id() RETURNS uuid LANGUAGE sql STABLE AS $$
 SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
$$;
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY[
  'tenant_rate_limits','devices','device_api_keys','app_users','telemetry_idempotency',
  'telemetry_events','alert_rules','rule_versions','suppression_policies','alerts',
  'alert_transitions','hourly_rollups','outbox_messages'] LOOP
  EXECUTE format('ALTER TABLE pulse.%I ENABLE ROW LEVEL SECURITY', t);
  EXECUTE format('ALTER TABLE pulse.%I FORCE ROW LEVEL SECURITY', t);
  EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON pulse.%I', t);
  EXECUTE format('CREATE POLICY tenant_isolation ON pulse.%I FOR ALL USING (tenant_id = pulse.current_tenant_id()) WITH CHECK (tenant_id = pulse.current_tenant_id())', t);
 END LOOP;
END $$;
-- Tenant records: only the current tenant row is visible to tenant-scoped operations.
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenants FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON tenants;
CREATE POLICY tenant_isolation ON tenants FOR ALL USING (tenant_id = pulse.current_tenant_id()) WITH CHECK (tenant_id = pulse.current_tenant_id());

