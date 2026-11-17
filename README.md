# Pulse
Local multi-tenant device telemetry and alerting with .NET 10, React/TypeScript, PostgreSQL and Redis. No Azure resources or Azure authentication.

Implemented: local Identity-format password hashing; JWT access and hashed rotating refresh tokens; platform/tenant administration; device registration and one-time API-key issuance/rotation/revocation; Redis quotas; atomic batch ingestion/idempotency/outbox; immutable rules; finalized event-time evaluation; alert transitions; SSE snapshots; rollups and retention. The dashboard includes the corresponding administration and telemetry screens.

**Verification is incomplete for the full stack:** Docker is absent on the implementation machine. The Docker/Npgsql/Redis integration test is checked in but was skipped. See [actual verification results](docs/verification.md), including the passing PostgreSQL WASM checks. Do not treat a successful build as proof that the complete stack has run.

## Requirements
- .NET 10 SDK (a newer SDK capable of targeting net10.0 also works).
- Node.js 22.12+ and npm 10+.
- Docker Desktop with Linux containers and Docker Compose v2.
- PowerShell 7 for the scripts below.
- Initial internet access to install packages and pull container images. Thereafter the local stack requires no cloud service.

The implementation machine had .NET SDK 11 with .NET 10 runtime, Node 14 and no Docker. Workspace-local Node 22 and npm 10 were used for validation without replacing the system Node installation.

## First run
From the repository root, generate local random secrets and load them into the current terminal:
```powershell
./infrastructure/local/New-LocalEnvironment.ps1
. ./infrastructure/local/Load-Environment.ps1
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d --wait
dotnet restore Pulse.slnx
dotnet run --project backend/Pulse.Api -- --migrate
dotnet run --project backend/Pulse.Api -- --bootstrap
```
The migration command uses the privileged Migration connection only. Bootstrap runs only while no users exist and creates a platform-admin from Bootstrap__Username and Bootstrap__Password. There is no HTTP bootstrap endpoint and no default password. Retrieve your generated password locally from the ignored .env; remove its Bootstrap__Password entry after successful bootstrap and clear that variable from your shell. Never share or commit .env. .env.example documents the configuration names with placeholders.

Start the API:
```powershell
dotnet run --project backend/Pulse.Api
```
In a second terminal:
```powershell
. ./infrastructure/local/Load-Environment.ps1
dotnet run --project backend/Pulse.Worker
```
In a third terminal:
```powershell
cd frontend/pulse-dashboard
npm ci
npm run dev
```
Open http://localhost:5173. The Vite proxy forwards /v1 and /health to http://localhost:5080.

1. Sign in as the bootstrapped platform-admin.
2. Create a tenant and then a tenant-admin assigned to it.
3. Sign out and sign in as the tenant-admin. Platform administrators have no implicit access to tenant telemetry.
4. Register a device; open Manage to issue a key. Copy it immediately; the secret is never retrievable.
5. Create users, rules and quotas in the tenant dashboard. Device names and enablement can be edited; keys can be rotated or revoked.

The API opens restricted App and Auth database connections and rejects superuser/BYPASSRLS configuration. The request-serving API does not use migration or maintenance connections during normal requests. Remove those environment variables from the API terminal after setup if desired. Only the maintenance worker uses the Maintenance connection.

## Submit telemetry
Use an issued key, a new event UUID and a UTC timestamp within the preceding 72 hours:
```powershell
$headers = @{ 'X-Device-Key' = '<identifier.secret>' }
$body = @{ items = @(@{ eventId = [guid]::NewGuid().ToString(); occurredAt = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o'); metric = 'temperature'; value = 42.5 }) } | ConvertTo-Json -Depth 4
Invoke-RestMethod http://localhost:5080/v1/telemetry -Method Post -ContentType application/json -Headers $headers -Body $body
```
Each valid batch contains 1–500 items and returns accepted/duplicate/rejected results. Quotas charge every submitted item in a structurally valid batch, including duplicates and event-level rejections. Redis failures return 503. A quota rejection returns 429 and Retry-After.

**Alert timing:** accepted telemetry is visible immediately. Rule evaluation finalizes after the 72-hour late-event watermark to make outcomes independent of arrival order. Threshold rules compare the window average; rate-of-change rules compare units per second. See [API semantics](docs/api/README.md). This is a deliberate finalized-alert model, not an immediate provisional-alert model.

## Build and test
```powershell
dotnet restore Pulse.slnx
dotnet build Pulse.slnx --no-restore
dotnet test Pulse.slnx --no-build
$env:PULSE_RUN_INTEGRATION='1'
dotnet test backend/Pulse.Tests.Integration
cd frontend/pulse-dashboard
npm ci
npm run build
npm test
npx playwright install chromium
npm run test:e2e
```
Integration tests launch a dedicated PostgreSQL container. They cover migrations, restricted roles, tenant isolation, duplicate concurrency, atomic records, key rotation, cross-tenant assignment denial and refresh-token concurrency. Without PULSE_RUN_INTEGRATION=1 the Docker test explicitly skips; a skipped test is not a pass.

Supplementary schema validation without Docker:
```powershell
cd database/verification
npm ci
npm test
```
This executes SQL in PostgreSQL WASM, including seeded RLS and transition checks. It does not verify Npgsql, real concurrent sessions, Redis or container networking. The SQL file concurrency_checks.sql inspects the unique constraint; true concurrent duplicate submission is covered by the Docker integration test.

## Schema and OpenAPI
- [Every table and column in the ERD](docs/database/erd.md).
- [Database authority, role boundaries and retention](docs/database/README.md).
- [SQL scripts](database/scripts/002_create_tables.sql).
- [Execution plan](docs/architecture/execution-plan.md).
- [Future Azure migration guide](docs/deployment/azure-migration-plan.md). Documentation only.

EF Core migrations are the deployment authority. Scripts 002–006 are embedded verbatim in the initial migration. Script 001 is cluster setup and must not run against the Compose-created database; Compose already creates the database and roles. Standalone scripts are for an empty alternative deployment and must never be applied on top of an EF-migrated schema.

The checked-in OpenAPI document generated the typed frontend client schema. To refresh it, start an explicitly local export instance:
```powershell
. ./infrastructure/local/Load-Environment.ps1
$env:ASPNETCORE_URLS='http://127.0.0.1:5081'
dotnet run --project backend/Pulse.Api -- --export-openapi
```
Then fetch http://127.0.0.1:5081/openapi/v1.json into docs/api/openapi.json and run npm run generate:api in the frontend directory. Stop that export instance afterward. The export flag makes OpenAPI anonymous for this explicit local generation step; normal instances require an administrator token. Export mode still secures all management routes.

## Optional replica test topology
After the host migration and bootstrap:
```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml -f infrastructure/docker/compose.replicas.yaml up -d --build
```
The local reverse proxy listens on http://localhost:5082 and distributes requests to two API replicas. SSE has buffering disabled. Connect authenticated streams through the proxy and transition an alert through another connection; both streams should resynchronize. Redis notifications are hints; durable PostgreSQL snapshots refresh every 15 seconds and on reconnect. The stream includes the latest 100 alerts; use keyset pagination for older history. Full replica behavior remains unverified on this machine.

## Operations
/health/live checks process liveness; /health/ready checks PostgreSQL and Redis connectivity. JSON logs include request trace/correlation identifiers; standard ASP.NET instrumentation and the Pulse Meter can be consumed by OpenTelemetry. No external telemetry exporter is configured.

PostgreSQL and Redis bind only to loopback in Compose. Named volumes persist data. Stop containers with docker compose down using the same files; do not remove volumes unless you intend to erase local data. Back up PostgreSQL before changing migrations or retention. Retention drops whole monthly partitions after 90 days, so physical raw retention can approach 121 days.
