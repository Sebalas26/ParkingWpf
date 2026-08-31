using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class BillingResolutionConfiguration : IEntityTypeConfiguration<BillingResolution>
{
    public void Configure(EntityTypeBuilder<BillingResolution> builder)
    {
        builder.ToTable("BillingResolutions");
        builder.HasKey(r => r.ResolutionId);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(150);
        builder.Property(r => r.DocumentType).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Prefix).HasMaxLength(20);
        builder.Property(r => r.ResolutionNumber).IsRequired().HasMaxLength(50);
    }
}
