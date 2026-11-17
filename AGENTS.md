# Pulse — Codex Development Instructions

## Mission
Build the complete Pulse multi-tenant device telemetry and alerting platform from scratch, running entirely on a local development machine. The backend is .NET 10 and the frontend is React + TypeScript. PostgreSQL and Redis run locally using Docker Compose. Do not create, provision, connect to, or require any Azure resources during this phase. The application must remain cloud-ready through configuration and replaceable infrastructure adapters.

## Non-negotiable rules
- Implement real working code, migrations, SQL scripts, automated tests, and documentation. Do not stop at scaffolding or produce mock-only features.
- Keep domain and application code independent of hosting, identity providers, secret stores, and cloud SDKs.
- All users, tenants, devices, roles, and API-key credentials are created and administered locally through secured backend APIs and persisted in PostgreSQL.
- Never store plaintext passwords or raw API-key secrets in PostgreSQL. Store salted password hashes (ASP.NET Core Identity) and keyed API-key digests. Show generated API-key secrets only once.
- No Microsoft Entra ID, Azure API Management, Application Gateway, Key Vault, Azure Monitor, Azure Container Apps, or Azure-managed PostgreSQL/Redis integration in the initial implementation.
- Avoid fake Azure services. Implement functional local equivalents behind clearly defined interfaces.
- No hardcoded secrets, real credentials, or committed .env files. Generate .env.example with safe placeholders and documented development-only setup.
- All data access must enforce tenant isolation; use PostgreSQL RLS plus authorization checks. Use transaction-local tenant context and an application role without BYPASSRLS. Separate privileged bootstrap/credential lookup from tenant-scoped data access.
- Preserve the approved functional requirements: batches up to 500, per-item results, concurrent idempotency, 72-hour late events, threshold and rate-of-change rules, immutable rule versions, alert state transitions and suppression, SSE across replicas, hourly rollups, retention, and keyset pagination.
- For every feature: implement, build, test, document; report actual results and blockers. Do not claim tests or Azure deployment succeeded unless verified.

## Local stack
- Backend: .NET 10 ASP.NET Core Web API, EF Core/Npgsql, .NET Worker Service, xUnit, Testcontainers, Serilog, OpenAPI.
- Frontend: React, TypeScript, Vite, React Router, TanStack Query, Material UI, Recharts, Vitest, Playwright.
- PostgreSQL: local Docker container; schema managed by EF Core migrations; checked-in reproducible SQL scripts matching migrations.
- Redis: local Docker container; StackExchange.Redis for atomic event quota and pub/sub.
- Authentication: ASP.NET Core Identity with locally stored users and roles; JWT access tokens, rotating refresh tokens stored hashed in PostgreSQL. Provide local admin bootstrap from an environment variable or interactive setup command, never a default password.
- Device authentication: one random API key per device, identifier + secret, keyed HMAC-SHA256 digest stored in PostgreSQL. Keyed HMAC secret loaded from local environment/configured secret provider; never stored in the database. Support revocation, expiration, rotation.
- Request throttling: local .NET middleware using Redis atomic counters keyed by authenticated tenant ID, 100 requests per tenant per 60 seconds; reject with 429 and Retry-After. Make the limiter replaceable by Azure APIM later.
- Event throttling: separate Redis atomic Lua counter keyed by authenticated tenant ID, 10,000 submitted events per tenant per 60 seconds; charge for every item in a structurally valid batch, including duplicates/rejected items. Redis unavailable => 503, fail closed.
- Local routing: React Vite dev proxy to ASP.NET Core API; no public load balancer or gateway required. Provide Docker Compose optional multi-replica test configuration with a local reverse proxy for SSE distribution tests.
- Logging: structured JSON logs, correlation IDs, /health/live, /health/ready, OpenTelemetry-compatible instrumentation, local console output.

## Repository structure
Pulse/
  AGENTS.md
  README.md
  Pulse.slnx
  backend/
    Pulse.Api/
    Pulse.Domain/
    Pulse.Application/
    Pulse.Infrastructure/
    Pulse.Worker/
    Pulse.Tests.Unit/
    Pulse.Tests.Integration/
  frontend/pulse-dashboard/
  database/
    scripts/
    migrations/
    seeds/
    verification/
  infrastructure/
    docker/
    local/
    azure/README.md
  docs/
    architecture/
    api/
    database/
    deployment/

## Database — generate scripts and ERD
Create at least these tables with full columns, UUID primary keys, foreign keys, indexes, unique constraints, timestamps, and appropriate tenant scoping:
1. tenants
2. tenant_rate_limits
3. app_users (ASP.NET Identity-compatible user model)
4. app_roles / app_user_roles and other required ASP.NET Identity tables
5. user_refresh_tokens
6. devices
7. device_api_keys
8. telemetry_events
9. telemetry_idempotency
10. alert_rules
11. rule_versions
12. suppression_policies
13. alerts
14. alert_transitions
15. hourly_rollups
16. outbox_messages

Generate database/scripts/001_create_roles_and_database.sql, 002_create_tables.sql, 003_indexes_constraints.sql, 004_rls.sql, 005_functions.sql, 006_partitions.sql; database/seeds/dev_seed.sql; database/verification/schema_checks.sql, tenant_isolation_checks.sql, concurrency_checks.sql; and matching EF Core migrations. EF Core migrations are the single deployment authority; standalone SQL scripts must match and must not be applied on top of an already migrated schema. Use a separate idempotency ledger with UNIQUE(tenant_id, device_id, event_id), independent of time-partitioned telemetry. Ensure accepted telemetry, ledger entries, and outbox records commit atomically. Partition raw telemetry monthly and implement partition maintenance and retention. Generate Mermaid ERD showing every table and column.

## Local user and tenant administration
Implement secured endpoints for local bootstrap and administration:
- POST /v1/auth/login, POST /v1/auth/refresh, POST /v1/auth/logout, GET /v1/auth/me
- POST /v1/admin/tenants, GET /v1/admin/tenants
- POST /v1/admin/users, GET /v1/admin/users, PATCH /v1/admin/users/{id}, POST /v1/admin/users/{id}/disable
- POST /v1/devices, GET /v1/devices, GET /v1/devices/{id}, PATCH /v1/devices/{id}
- POST /v1/devices/{id}/keys, POST /v1/devices/{id}/keys/{keyId}/revoke
- GET/PATCH /v1/admin/tenant-rate-limits
Use explicit platform-admin bootstrap for tenant provisioning; tenant-admin, operator, viewer roles for tenant operations. Authenticate and authorize all management routes. Prevent cross-tenant user/device assignment. Include a local React admin UI for user creation, tenant creation, device registration, key rotation and tenant quota configuration.

## Telemetry ingestion
Implement POST /v1/telemetry with 1–500 items, authenticated per-device keys, event-time validation including 72-hour lateness, per-item accepted/duplicate/rejected results, and device ownership enforcement. A single invalid event cannot fail the whole valid batch. For structurally invalid batches return 400. Use DB constraints for concurrency-safe idempotency. A valid batch returns 200 with item statuses. Never trust tenantId from client payload. Use DB transaction-local RLS context. Persist outbox work atomically for accepted events.

## Rules, alerts, workers and dashboard
- Threshold and rate-of-change rules over event-time windows; immutable rule versions; deterministic handling of late events; atomic suppression; alert OPEN -> ACKNOWLEDGED -> RESOLVED (also OPEN -> RESOLVED), reject invalid transitions.
- Alert listing with filters and keyset pagination on (created_at, alert_id).
- Separate .NET workers for outbox processing, rule evaluation, hourly rollups, and retention. Make processing idempotent and retryable.
- SSE /v1/alerts/stream with tenant authorization and Redis Pub/Sub across API replicas. Pub/Sub is not durable; rely on PostgreSQL outbox and replay strategy.
- React pages: login, overview, tenant/user administration, devices, API keys, telemetry charts, rules and versions, alerts and transitions, live feed, settings. Use OpenAPI-generated typed API client, loading/error/empty states, RBAC-aware UI, tests.

## Azure-ready seam — DO NOT implement Azure yet
Define and use these interfaces in Application or Infrastructure abstractions:
- IUserIdentityService / ITokenIssuer: LocalIdentityProvider now; later Entra/JWT validation adapter.
- IDeviceCredentialValidator: PostgreSqlDeviceCredentialValidator now; later gateway validation integration, retaining DB-backed keys if desired.
- ISecretProvider: EnvironmentSecretProvider now; later AzureKeyVaultSecretProvider.
- IRequestRateLimiter: RedisRequestRateLimiter in local API middleware now; later APIM enforces requests at edge and backend middleware can be disabled/configured as defense in depth.
- IEventRateLimiter: RedisEventRateLimiter now; later Azure Managed Redis with same contract.
- INotificationBus: RedisNotificationBus now; later managed Redis or another broker adapter.
- ITelemetryRepository / application DbContext: local PostgreSQL now; later Azure PostgreSQL via connection string and network configuration.
- IObservability: OpenTelemetry/structured local logs now; later Azure Monitor exporter.
- Hosting: local Kestrel/Docker now; later Azure Container Apps via infrastructure deployment.
Use appsettings.Local.json and environment-based configuration with strongly typed options. Never add Azure-specific code to Domain or Application. Document provider mapping in docs/deployment/azure-migration-plan.md. Include a future-only infrastructure/azure/README.md describing the migration path, but do not provision or require Azure resources.

## Configuration example
{
  "Hosting": {"Provider": "Local"},
  "Identity": {"Provider": "Local"},
  "Secrets": {"Provider": "Environment"},
  "RateLimiting": {"RequestProvider": "Redis", "EventProvider": "Redis", "RequestsPerMinute": 100, "EventsPerMinute": 10000},
  "Notifications": {"Provider": "Redis"},
  "Database": {"Provider": "PostgreSQL"},
  "Observability": {"Provider": "OpenTelemetry"}
}
This is an illustrative configuration fragment; secrets and connection strings must come from local environment variables or development secret storage.

## Delivery phases
1. Inspect repository and write concise plan.
2. Scaffold solution, React app, Docker Compose, configuration, and health endpoints.
3. Generate database schema, EF migrations, reproducible SQL scripts, seeds, ERD, RLS and tests.
4. Implement local user registration/admin creation, JWT auth, role permissions, device API-key issuance and verification.
5. Implement Redis request/event limits and telemetry ingestion/idempotency.
6. Implement rules, alerts, workers, rollups, retention, outbox and SSE.
7. Implement React UI and API integration.
8. Add unit/integration/E2E/concurrency/security tests, documentation, local run scripts.
9. Write Azure migration guide only; do not create cloud resources or enable Azure integrations.

## Verification and completion
Run dotnet restore/build/test, npm install/build/test, Docker Compose health checks, migrations, schema checks, cross-tenant tests, duplicate/concurrency tests, and frontend E2E tests where available. Report exact commands and results. Never claim tests passed when they have not run. Keep the application runnable offline except for initial package installation.

## Agent execution prompt
Read AGENTS.md. Build Pulse locally from scratch. Begin with scaffolding and PostgreSQL database scripts/migrations/ERD, then implement local user/tenant/device administration and secure API-key issuance. No Azure usage. Provide an execution plan, implement in verifiable phases, run tests, and report actual results. Do not deploy to cloud services.
