namespace Pulse.Domain.Identity;

public class Role
{
    public Guid RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}
