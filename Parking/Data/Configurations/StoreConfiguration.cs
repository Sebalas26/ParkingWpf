using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores");
        builder.HasKey(s => s.StoreId);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(120);
        builder.Property(s => s.TaxId).IsRequired().HasMaxLength(50);
        builder.HasIndex(s => s.TaxId).IsUnique();

        builder.Property(s => s.PhoneNumber).HasMaxLength(30);
    }
}
