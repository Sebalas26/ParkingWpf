using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class AppPermissionConfiguration : IEntityTypeConfiguration<AppPermission>
{
    public void Configure(EntityTypeBuilder<AppPermission> builder)
    {
        builder.ToTable("AppPermissions");
        builder.HasKey(p => p.PermissionId);

        builder.Property(p => p.ActionKey).IsRequired().HasMaxLength(50);
        builder.Property(p => p.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(255);

        builder.HasIndex(p => new { p.ModuleId, p.ActionKey }).IsUnique();

        builder.HasOne(p => p.Module)
            .WithMany(m => m.Permissions)
            .HasForeignKey(p => p.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
