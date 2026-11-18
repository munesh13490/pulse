namespace Pulse.Application.Services;

public sealed class UserService(IUserRepository users, IPasswordService passwords) : IUserService
{
    public Task<Guid> CreateAsync(UserIdentity actor, Guid? tenant, string name, string password, string role, CancellationToken ct)
    {
        var platform = actor.Roles.Contains("platform-admin");
        if (!platform && (!actor.Roles.Contains("tenant-admin") || tenant != actor.TenantId))
            throw new UnauthorizedAccessException();
        if (role == "platform-admin" && !platform)
            throw new ArgumentException("Invalid role");
        UserValidator.Validate(tenant, name, password, role);
        return CreateUserAsync(new(Guid.NewGuid(), tenant, name, passwords.Hash(name, password), role), ct);
    }

    private async Task<Guid> CreateUserAsync(UserRegistration user, CancellationToken ct)
    {
        await users.CreateAsync(user, ct);
        return user.Id;
    }

    public Task<List<Dictionary<string, object?>>> ListAsync(UserIdentity actor, CancellationToken ct) => users.ListAsync(actor, ct);
    public Task<bool> UpdateAsync(UserIdentity actor, Guid id, string? name, bool disable, CancellationToken ct)
    {
        if (name is not null)
            NameValidator.Validate(name);
        if (id == actor.Id && disable)
            throw new ArgumentException("Cannot disable yourself.");
        return users.UpdateAsync(actor, id, name, disable, ct);
    }
}
