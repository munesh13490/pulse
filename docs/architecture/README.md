# Local architecture

See [backend folder and dependency guide](backend-structure.md) for the segregated .NET structure and conventions.
Domain contains validation, rule math and alert-state policy. Application defines provider contracts and DTOs. Infrastructure provides PostgreSQL, Identity hashing, JWT, environment secrets, Redis quotas/pubsub and SQL-first EF migrations. API applies authentication, role checks and tenant scopes. Worker hosts independent retryable evaluation, outbox, rollup and retention services.

All tenant data access uses pulse_app and a transaction-local tenant setting. Identity administration and credential lookup use pulse_auth. Device key lookup is a narrowly granted SECURITY DEFINER function. Migration/maintenance credentials are excluded from request-serving paths. No Azure SDK is referenced.

An ingestion transaction obtains a per-tenant event-time advisory lock, validates the batch, inserts a unique independent ledger row, telemetry and an outbox message atomically. The database constraint handles concurrent duplicates. Worker finalization shares the lock and orders closed events by event-time/event-ID. Atomic suppression updates prevent duplicate suppressed alerts across replicas. Immutable rule versions are selected by event time. Transition updates and history/outbox writes share a transaction.

Redis counters are atomic Lua increments with a 60-second TTL. Quotas are shared across replicas. Pub/Sub wakes connected SSE clients; persisted alert snapshots recover lost wakeups. Outbox publication is at least once and the UI replaces records by ID. Alert history remains available through keyset paging.

Local retention needs privileged DDL credentials to maintain/drop partitions. It runs independently from the API. All local infrastructure choices are replaceable through Application contracts and dependency injection.
