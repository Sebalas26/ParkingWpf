using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class VehicleRateConfiguration : IEntityTypeConfiguration<VehicleRate>
{
    public void Configure(EntityTypeBuilder<VehicleRate> builder)
    {
        builder.ToTable("VehicleRates");
        builder.HasKey(r => r.RateId);

        builder.Property(r => r.DisplayName).IsRequired().HasMaxLength(60);
        builder.Property(r => r.IconKey).IsRequired().HasMaxLength(50);

        builder.Property(r => r.MinuteRate).HasPrecision(18, 2);
        builder.Property(r => r.HourRate).HasPrecision(18, 2);
        builder.Property(r => r.FullDayRate).HasPrecision(18, 2);

        builder.HasIndex(r => new { r.BranchId, r.VehicleType });
    }
}
