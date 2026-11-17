CREATE FUNCTION maintain_telemetry_partitions() RETURNS void LANGUAGE plpgsql AS $$
DECLARE start_month date; partition_name text;
BEGIN
 FOR i IN -1..2 LOOP
 start_month := (date_trunc('month',now()) + make_interval(months=>i))::date;
 partition_name := 'telemetry_events_' || to_char(start_month,'YYYY_MM');
 EXECUTE format('CREATE TABLE IF NOT EXISTS %I PARTITION OF telemetry_events FOR VALUES FROM (%L) TO (%L)',partition_name,start_month,start_month+interval '1 month');
 EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',partition_name);
 EXECUTE format('REVOKE ALL ON %I FROM pulse_app,pulse_auth',partition_name);
 END LOOP;
END; $$;
REVOKE ALL ON FUNCTION maintain_telemetry_partitions() FROM PUBLIC;
SELECT maintain_telemetry_partitions();
