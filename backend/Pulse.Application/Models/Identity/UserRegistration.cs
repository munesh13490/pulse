namespace Pulse.Application.Models.Identity;

public sealed record UserRegistration(Guid Id, Guid? TenantId, string Name, string PasswordHash, string Role);
