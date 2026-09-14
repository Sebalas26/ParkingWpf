using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class CustomerVehicleConfiguration : IEntityTypeConfiguration<CustomerVehicle>
{
    public void Configure(EntityTypeBuilder<CustomerVehicle> builder)
    {
        builder.ToTable("CustomerVehicles");
        builder.HasKey(cv => cv.Id);

        builder.Property(cv => cv.PlateNumber).IsRequired().HasMaxLength(20);

        builder.HasOne(cv => cv.Customer)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(cv => cv.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cv => new { cv.CustomerId, cv.PlateNumber }).IsUnique();
        builder.HasIndex(cv => cv.PlateNumber);
    }
}
