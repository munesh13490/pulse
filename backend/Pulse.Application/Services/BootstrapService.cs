namespace Pulse.Application.Services;

public sealed class BootstrapService(IUserRepository users, IPasswordService passwords) : IBootstrapService
{
    public Task BootstrapAsync(string name, string password, CancellationToken ct)
    {
        UserValidator.Validate(null, name, password, "platform-admin");
        return users.BootstrapAsync(new(Guid.NewGuid(), null, name, passwords.Hash(name, password), "platform-admin"), ct);
    }
}
