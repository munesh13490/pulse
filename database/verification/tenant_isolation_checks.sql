BEGIN;
SET LOCAL ROLE pulse_app;
SELECT set_config('pulse.tenant_id',gen_random_uuid()::text,true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM devices) THEN RAISE EXCEPTION 'Tenant data leaked'; END IF; END $$;
ROLLBACK;
