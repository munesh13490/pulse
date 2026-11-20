using Microsoft.EntityFrameworkCore;
using Pulse.Domain.Identity;

namespace Pulse.Infrastructure.Persistence;

public sealed class PulseDbContext(DbContextOptions<PulseDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var roles = modelBuilder.Entity<Role>();
        roles.ToTable("roles", "pulse");
        roles.HasKey(role => role.RoleId);
        roles.Property(role => role.RoleId).HasColumnName("role_id");
        roles.Property(role => role.RoleCode).HasColumnName("role_code").HasMaxLength(50);
        roles.Property(role => role.RoleName).HasColumnName("role_name").HasMaxLength(100);
        roles.Property(role => role.Description).HasColumnName("description");
        roles.Property(role => role.IsSystemRole).HasColumnName("is_system_role").HasDefaultValue(true);
        roles.Property(role => role.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        var users = modelBuilder.Entity<AppUser>();
        users.ToTable("app_users", "pulse");
        users.HasKey(user => user.UserId);
        users.Property(user => user.UserId).HasColumnName("user_id");
        users.Property(user => user.TenantId).HasColumnName("tenant_id");
        users.Property(user => user.Email).HasColumnName("email").HasMaxLength(320);
        users.Property(user => user.PasswordHash).HasColumnName("password_hash");
        users.Property(user => user.RoleId).HasColumnName("role_id");
        users.HasOne(user => user.Role).WithMany(role => role.Users)
            .HasForeignKey(user => user.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
