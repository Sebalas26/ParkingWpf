using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class ParkingTicketConfiguration : IEntityTypeConfiguration<ParkingTicket>
{
    public void Configure(EntityTypeBuilder<ParkingTicket> builder)
    {
        builder.ToTable("ParkingTickets");
        builder.HasKey(t => t.TicketId);

        builder.Property(t => t.TicketNumber).IsRequired().HasMaxLength(60);
        builder.HasIndex(t => t.TicketNumber).IsUnique();

        builder.Property(t => t.PlateNumber).IsRequired().HasMaxLength(20);
        builder.Property(t => t.CustomerPhone).HasMaxLength(30);
        builder.Property(t => t.BayNumber).HasMaxLength(30);
        builder.Property(t => t.Notes).HasMaxLength(255);
        builder.Property(t => t.OperatorName).IsRequired().HasMaxLength(100);

        builder.Property(t => t.HourlyRate).HasPrecision(18, 2);
        builder.Property(t => t.GrossAmount).HasPrecision(18, 2);
        builder.Property(t => t.DiscountAmount).HasPrecision(18, 2);
        builder.Property(t => t.NetAmount).HasPrecision(18, 2);
        builder.Property(t => t.AmountPaid).HasPrecision(18, 2);
        builder.Property(t => t.ChangeGiven).HasPrecision(18, 2);

        builder.HasIndex(t => new { t.PlateNumber, t.Status });
        builder.HasIndex(t => t.EntryTimeUtc);
    }
}
