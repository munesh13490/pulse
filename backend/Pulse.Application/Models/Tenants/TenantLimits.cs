namespace Pulse.Application.Models.Tenants;

public sealed record TenantLimits(int RequestsPerMinute = 100, int EventsPerMinute = 10000);
