using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class MonthlySubscriptionConfiguration : IEntityTypeConfiguration<MonthlySubscription>
{
    public void Configure(EntityTypeBuilder<MonthlySubscription> builder)
    {
        builder.ToTable("MonthlySubscriptions");
        builder.HasKey(s => s.SubscriptionId);

        builder.Property(s => s.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(s => s.CustomerDocument).IsRequired().HasMaxLength(50);
        builder.Property(s => s.CustomerPhone).IsRequired().HasMaxLength(30);
        builder.Property(s => s.CustomerEmail).HasMaxLength(100);
        builder.Property(s => s.PlateNumber).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Notes).HasMaxLength(255);

        builder.Property(s => s.MonthlyFee).HasPrecision(18, 2);
        builder.Property(s => s.AmountPaid).HasPrecision(18, 2);

        builder.HasIndex(s => s.PlateNumber);
        builder.HasIndex(s => new { s.PlateNumber, s.IsActive, s.EndDateUtc });
    }
}
