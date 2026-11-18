using System.Security.Claims;

namespace Pulse.Api.Security;

public static class RequestIdentity
{
    public static Guid Tenant(HttpContext context) => Guid.Parse(context.User.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException());
    public static Guid UserId(HttpContext context) => Guid.Parse(context.User.FindFirstValue("sub")!);
    public static async Task<UserIdentity> Actor(HttpContext context, IUserIdentityService identities, CancellationToken ct) => await identities.Find(UserId(context), ct) ?? throw new UnauthorizedAccessException();
}
