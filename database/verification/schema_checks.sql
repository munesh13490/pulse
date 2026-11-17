DO $$ BEGIN
IF (SELECT rolbypassrls OR rolsuper FROM pg_roles WHERE rolname='pulse_app') THEN RAISE EXCEPTION 'Unsafe app role'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='tenant_rate_limits' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: tenant_rate_limits'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='devices' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: devices'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='device_api_keys' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: device_api_keys'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='telemetry_events' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: telemetry_events'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='telemetry_idempotency' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: telemetry_idempotency'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='alert_rules' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: alert_rules'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='rule_versions' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: rule_versions'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='suppression_policies' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: suppression_policies'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='alerts' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: alerts'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='alert_transitions' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: alert_transitions'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='hourly_rollups' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: hourly_rollups'; END IF;
IF NOT EXISTS(SELECT 1 FROM pg_class WHERE relname='outbox_messages' AND relrowsecurity AND relforcerowsecurity) THEN RAISE EXCEPTION 'RLS missing: outbox_messages'; END IF;
END $$;
