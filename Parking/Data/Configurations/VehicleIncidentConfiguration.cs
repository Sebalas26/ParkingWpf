using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class VehicleIncidentConfiguration : IEntityTypeConfiguration<VehicleIncident>
{
    public void Configure(EntityTypeBuilder<VehicleIncident> builder)
    {
        builder.ToTable("VehicleIncidents");
        builder.HasKey(i => i.IncidentId);

        builder.Property(i => i.PlateNumber).IsRequired().HasMaxLength(15);
        builder.Property(i => i.IncidentType).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(500);
        builder.Property(i => i.Status).IsRequired().HasMaxLength(30);
        builder.Property(i => i.ReportedBy).IsRequired().HasMaxLength(100);
        builder.Property(i => i.ResolvedBy).HasMaxLength(100);
        builder.Property(i => i.ResolvedNotes).HasMaxLength(500);

        builder.HasIndex(i => i.PlateNumber);
        builder.HasIndex(i => new { i.BranchId, i.IsBlocked, i.Status });
    }
}
