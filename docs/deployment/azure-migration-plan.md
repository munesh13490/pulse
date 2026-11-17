# Future provider mapping
This repository provisions and connects to no Azure resources.
Domain and Application contain no hosting or provider SDK dependencies.
| Contract | Local adapter | Future replacement |
|---|---|---|
| IUserIdentityService, ITokenIssuer | LocalIdentityProvider, JwtTokenIssuer | External identity adapter after authorization design review |
| ISecretProvider | EnvironmentSecretProvider | Secret-store adapter |
| IDeviceCredentialValidator | PostgreSqlDeviceCredentialValidator | Gateway integration retaining database lifecycle |
| IRequestRateLimiter | RedisRequestRateLimiter | Edge quota policy plus configurable backend defense |
| IEventRateLimiter | RedisEventRateLimiter | Redis connection configuration |
| INotificationBus | RedisNotificationBus | Managed broker adapter |
| ITelemetryRepository | TelemetryRepository | PostgreSQL connection configuration |
| IObservability | LocalObservability | OpenTelemetry exporter |
Kestrel and the worker can later be containerized. Migrations remain a separate privileged deployment step. Preserve restricted application credentials, transaction-local RLS, local-user migration policy, key HMAC secret continuity, durable outbox and SSE resynchronization semantics.
No cloud authentication, secret provider or provisioning code is implemented.
