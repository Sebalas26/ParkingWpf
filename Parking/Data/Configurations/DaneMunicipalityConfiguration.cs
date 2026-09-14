using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class DaneMunicipalityConfiguration : IEntityTypeConfiguration<DaneMunicipality>
{
    public void Configure(EntityTypeBuilder<DaneMunicipality> builder)
    {
        builder.ToTable("DaneMunicipalities");
        builder.HasKey(m => m.Code);

        builder.Property(m => m.Code).HasMaxLength(10);
        builder.Property(m => m.DepartmentCode).IsRequired().HasMaxLength(10);
        builder.Property(m => m.DepartmentName).IsRequired().HasMaxLength(100);
        builder.Property(m => m.MunicipalityName).IsRequired().HasMaxLength(150);

        builder.HasIndex(m => m.DepartmentCode);
        builder.HasIndex(m => m.MunicipalityName);
    }
}
