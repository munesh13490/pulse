using Pulse.Domain;
namespace Pulse.Application;

public record UserIdentity(Guid Id, Guid? TenantId, string Name, string[] Roles);
public record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
public record DeviceIdentity(Guid TenantId, Guid DeviceId);
public interface ISecretProvider { string Get(string name); }
public interface ITokenIssuer { string Issue(UserIdentity user); }
public interface IUserIdentityService
{
    Task<TokenPair?> Login(string username, string password, CancellationToken ct);
    Task<TokenPair?> Refresh(string token, CancellationToken ct);
    Task Logout(string token, CancellationToken ct);
    Task<UserIdentity?> Find(Guid id, CancellationToken ct);
}
public interface IDeviceCredentialValidator { Task<DeviceIdentity?> Validate(string key, CancellationToken ct); }
public interface IRequestRateLimiter { Task<bool> Allow(Guid tenant, int count, int limit); }
public interface IEventRateLimiter { Task<bool> Allow(Guid tenant, int count, int limit); }
public interface INotificationBus { Task Publish(Guid tenant, string message); }
public interface ITelemetryRepository
{
    Task<IReadOnlyList<ItemResult>> Ingest(DeviceIdentity device, IReadOnlyList<TelemetryItem> items, CancellationToken ct);
}
public interface IObservability { void Count(string operation); }
