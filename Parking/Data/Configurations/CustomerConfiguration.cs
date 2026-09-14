using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.CustomerId);

        builder.Property(c => c.DocumentNumber).IsRequired().HasMaxLength(50);
        builder.Property(c => c.CheckDigit).HasMaxLength(2);
        builder.Property(c => c.PersonType).IsRequired().HasMaxLength(20);
        builder.Property(c => c.FullName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TradeName).HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Address).HasMaxLength(250);
        builder.Property(c => c.CityCode).HasMaxLength(10);
        builder.Property(c => c.StateCode).HasMaxLength(10);
        builder.Property(c => c.FiscalResponsibilities).IsRequired().HasMaxLength(50);

        builder.HasIndex(c => new { c.CompanyId, c.DocumentNumber }).IsUnique();
        builder.HasIndex(c => c.FullName);
    }
}
