CREATE FUNCTION lookup_device_key(key_id uuid)
RETURNS TABLE(tenant_id uuid,device_id uuid,digest bytea,expires_at timestamptz)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT k.tenant_id,k.device_id,k.digest,k.expires_at FROM public.device_api_keys k
 JOIN public.devices d ON d.id=k.device_id AND d.tenant_id=k.tenant_id
 WHERE k.id=key_id AND k.revoked_at IS NULL AND k.expires_at>now() AND d.enabled;
$$;
REVOKE ALL ON FUNCTION lookup_device_key(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION lookup_device_key(uuid) TO pulse_auth;
CREATE FUNCTION immutable_rule_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Rule versions are immutable'; END; $$;
CREATE TRIGGER rule_version_immutable BEFORE UPDATE OR DELETE ON rule_versions FOR EACH ROW EXECUTE FUNCTION immutable_rule_version();
CREATE FUNCTION valid_alert_transition() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.state<>OLD.state AND NOT ((OLD.state='OPEN' AND NEW.state IN ('ACKNOWLEDGED','RESOLVED')) OR (OLD.state='ACKNOWLEDGED' AND NEW.state='RESOLVED')) THEN
 RAISE EXCEPTION 'Invalid alert transition' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER alert_state BEFORE UPDATE ON alerts FOR EACH ROW EXECUTE FUNCTION valid_alert_transition();
