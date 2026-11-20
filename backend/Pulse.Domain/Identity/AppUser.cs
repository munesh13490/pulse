namespace Pulse.Domain.Identity;

public class AppUser
{
    public Guid UserId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; } = null!;
}
