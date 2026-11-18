using Pulse.Tests.Unit.Fakes;
using Xunit;

namespace Pulse.Tests.Unit.Services;

public sealed class UserServiceTests
{
    [Fact]
    public async Task TenantAdminCannotCreateUserInAnotherTenant()
    {
        var repository = new RecordingUserRepository();
        var service = new UserService(repository, new IdentityPasswordService());
        var actor = new UserIdentity(Guid.NewGuid(), Guid.NewGuid(), "admin", ["tenant-admin"]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(actor, Guid.NewGuid(), "viewer", "sufficient-password", "viewer", default));
        Assert.Empty(repository.Created);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task NonAdministratorsCannotCreateUsers(string role)
    {
        var repository = new RecordingUserRepository();
        var service = new UserService(repository, new IdentityPasswordService());
        var actor = new UserIdentity(Guid.NewGuid(), Guid.NewGuid(), "user", [role]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(actor, actor.TenantId, "viewer", "sufficient-password", "viewer", default));
        Assert.Empty(repository.Created);
    }

    [Fact]
    public async Task UserCreationPassesOnlyPasswordHashToRepository()
    {
        var repository = new RecordingUserRepository();
        var service = new UserService(repository, new IdentityPasswordService());
        var actor = new UserIdentity(Guid.NewGuid(), Guid.NewGuid(), "admin", ["tenant-admin"]);
        const string password = "sufficient-test-password";
        var id = await service.CreateAsync(actor, actor.TenantId, "viewer", password, "viewer", default);
        var user = Assert.Single(repository.Created);
        Assert.Equal(id, user.Id);
        Assert.Equal(actor.TenantId, user.TenantId);
        Assert.NotEqual(password, user.PasswordHash);
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<string>();
        Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user.Name, user.PasswordHash, password));
    }

    [Fact]
    public async Task AdministratorCannotDisableSelf()
    {
        var repository = new RecordingUserRepository();
        var service = new UserService(repository, new IdentityPasswordService());
        var actor = new UserIdentity(Guid.NewGuid(), Guid.NewGuid(), "admin", ["tenant-admin"]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(actor, actor.Id, null, true, default));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task TenantAdminCannotCreatePlatformAdministrator()
    {
        var repository = new RecordingUserRepository();
        var service = new UserService(repository, new IdentityPasswordService());
        var actor = new UserIdentity(Guid.NewGuid(), Guid.NewGuid(), "admin", ["tenant-admin"]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(actor, actor.TenantId, "admin2", "sufficient-password", "platform-admin", default));
        Assert.Empty(repository.Created);
    }
}
