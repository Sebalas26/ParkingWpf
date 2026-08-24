using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class AppModuleConfiguration : IEntityTypeConfiguration<AppModule>
{
    public void Configure(EntityTypeBuilder<AppModule> builder)
    {
        builder.ToTable("AppModules");
        builder.HasKey(m => m.ModuleId);

        builder.Property(m => m.ModuleKey).IsRequired().HasMaxLength(50);
        builder.HasIndex(m => m.ModuleKey).IsUnique();

        builder.Property(m => m.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(m => m.IconKey).IsRequired().HasMaxLength(50);
        builder.Property(m => m.Description).HasMaxLength(255);
    }
}
