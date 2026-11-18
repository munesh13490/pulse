using Pulse.Domain;

namespace Pulse.Application.Models.Identity;

public record UserIdentity(Guid Id, Guid? TenantId, string Name, string[] Roles);
