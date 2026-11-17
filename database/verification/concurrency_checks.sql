-- Run concurrent duplicate ingestion test in Pulse.Tests.Integration.
SELECT conname,pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='telemetry_idempotency'::regclass AND contype='u';
