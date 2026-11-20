SET LOCAL search_path TO pulse, public;
INSERT INTO roles(role_code,role_name) VALUES
 ('PLATFORM_ADMIN','Platform Administrator'),('ADMIN','Tenant Administrator'),
 ('OPERATOR','Operator'),('VIEWER','Viewer')
ON CONFLICT (role_code) DO NOTHING;

CREATE TABLE IF NOT EXISTS user_refresh_tokens (
 id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app_users(user_id) ON DELETE CASCADE,
 token_hash text NOT NULL UNIQUE, expires_at timestamptz NOT NULL,
 revoked_at timestamptz, created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE suppression_policies ADD COLUMN IF NOT EXISTS last_alert_at timestamptz;
CREATE UNIQUE INDEX IF NOT EXISTS ux_alerts_source_event
 ON alerts(tenant_id,device_id,rule_version_id,source_event_id);
