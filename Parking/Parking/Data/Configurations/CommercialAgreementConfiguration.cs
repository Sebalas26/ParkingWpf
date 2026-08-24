using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class CommercialAgreementConfiguration : IEntityTypeConfiguration<CommercialAgreement>
{
    public void Configure(EntityTypeBuilder<CommercialAgreement> builder)
    {
        builder.ToTable("CommercialAgreements");
        builder.HasKey(a => a.AgreementId);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(120);
        builder.Property(a => a.MinPurchaseAmount).HasPrecision(18, 2);
        builder.Property(a => a.DiscountPercentage).HasPrecision(5, 2);
        builder.Property(a => a.DiscountFixedAmount).HasPrecision(18, 2);

        builder.HasOne(a => a.Store)
            .WithMany(s => s.Agreements)
            .HasForeignKey(a => a.StoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
