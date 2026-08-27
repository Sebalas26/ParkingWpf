using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class VehicleIncidentBranchConfiguration : IEntityTypeConfiguration<VehicleIncidentBranch>
{
    public void Configure(EntityTypeBuilder<VehicleIncidentBranch> builder)
    {
        builder.ToTable("VehicleIncidentBranches");
        builder.HasKey(ib => new { ib.IncidentId, ib.BranchId });

        builder.HasOne(ib => ib.VehicleIncident)
            .WithMany(i => i.IncidentBranches)
            .HasForeignKey(ib => ib.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
