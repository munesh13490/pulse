# Verification results

Verified locally on 17 November 2026. No cloud resources were created or used by the application.

| Check | Actual result |
|---|---|
| dotnet restore Pulse.slnx | Passed after downloading compatible patched dependencies with network permission |
| dotnet build Pulse.slnx --no-restore | Passed, 0 warnings and 0 errors, targeting net10.0 with SDK 11.0.100 |
| dotnet test Pulse.slnx --no-build | 24 unit tests passed; 1 Docker integration test explicitly skipped |
| npm install in frontend/pulse-dashboard | Passed using workspace-local Node 22.16.0 and npm 10.9.2 |
| npm run generate:api | Passed against the document exported by the running API |
| npm run build in frontend/pulse-dashboard | Passed (TypeScript and Vite) |
| npm test in frontend/pulse-dashboard | 3 transport tests passed |
| npm run test:e2e in frontend/pulse-dashboard | 1 Chromium test passed |
| node database/verification/verify.mjs | All 5 schema scripts and 3 SQL verification scripts passed in PostgreSQL WASM; seeded behavioral assertions passed |
| GET /health/live | HTTP 200 with status live |
| Anonymous GET /v1/admin/tenants | HTTP 401 as expected |
| PowerShell setup script syntax | Passed PowerShell parser checks |
| GET /openapi/v1.json in explicit local export mode | HTTP 200; generated document and frontend types checked in |
| Docker Compose health, EF migration application, native schema checks | Not run: Docker executable/engine absent |
| Testcontainers concurrency/RLS/key/refresh integration | Skipped: requires Docker and PULSE_RUN_INTEGRATION=1 |
| Real Redis quota/failure and cross-replica SSE checks | Not run: local Redis/Docker unavailable |
| Authenticated whole-stack browser workflows | Not run: PostgreSQL/Redis local stack unavailable |

Frontend commands were executed with .local/node.exe and .local/package/bin/npm-cli.js, prepending .local to PATH; system Node 14 was left unchanged. Chromium was downloaded and run with process permissions outside the sandbox. The login browser test intercepts the failed login HTTP response; it verifies rendering, form interaction and error display, not real password authentication.

The PostgreSQL WASM verifier executes real SQL, with seeded assertions for forced RLS, cross-tenant write rejection, transaction-local context reset, duplicate ledger insertion, monthly partition routing, immutable rule versions and valid/invalid alert transitions. It does not run EF/Npgsql or simultaneous database sessions. concurrency_checks.sql inspects the unique constraint; the unrun Testcontainers test is the actual concurrent-ingestion check.

EF script generation was verified without connecting to PostgreSQL. A unit test confirms that all five schema scripts are embedded in migration order and that EF discovers the migration and generates its schema, RLS and partition functions. Applying that migration to native PostgreSQL remains unverified.

An early package audit reported vulnerable transitive dependencies. Compatible patched versions were selected and restore passed without suppressing the audit. Initial frontend build/test issues (OpenAPI response metadata, Vitest suite selection and Playwright required-field label matching) were corrected before the passing runs recorded here.

## Run the remaining checks

Install Docker Desktop with Linux containers. Follow the first-run commands in README.md, then:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml ps
$env:PULSE_RUN_INTEGRATION='1'
dotnet test backend/Pulse.Tests.Integration
```

Run database/verification/schema_checks.sql and tenant_isolation_checks.sql against the migrated local database as the cluster administrator. Then exercise a mixed accepted/duplicate/rejected batch and submit the same event concurrently. Confirm the ledger, raw telemetry and outbox each contain exactly one accepted record.

Start the optional replica topology and validate authenticated SSE snapshots through the reverse proxy, alert transitions, Redis interruption (503), quotas (429/Retry-After), worker retries and partition maintenance. Those behaviors have working implementations but remain integration verification work.
