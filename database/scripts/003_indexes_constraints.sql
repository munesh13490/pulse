CREATE UNIQUE INDEX device_one_active_key ON device_api_keys(device_id) WHERE revoked_at IS NULL;
CREATE INDEX telemetry_query ON telemetry_events(tenant_id,device_id,metric,occurred_at DESC);
CREATE INDEX alerts_page ON alerts(tenant_id,created_at DESC,id DESC);
CREATE INDEX outbox_pending ON outbox_messages(available_at,created_at) WHERE processed_at IS NULL;
CREATE INDEX refresh_user ON user_refresh_tokens(user_id);
CREATE INDEX device_tenant ON devices(tenant_id);
CREATE INDEX ledger_created ON telemetry_idempotency(created_at);
