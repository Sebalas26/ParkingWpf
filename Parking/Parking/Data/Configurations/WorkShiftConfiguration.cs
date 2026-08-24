using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class WorkShiftConfiguration : IEntityTypeConfiguration<WorkShift>
{
    public void Configure(EntityTypeBuilder<WorkShift> builder)
    {
        builder.ToTable("WorkShifts");
        builder.HasKey(ws => ws.ShiftId);

        builder.Property(ws => ws.OperatorName).IsRequired().HasMaxLength(100);
        builder.Property(ws => ws.BaseAmount).HasPrecision(18, 2);
        builder.Property(ws => ws.TotalCashCollected).HasPrecision(18, 2);
        builder.Property(ws => ws.TotalCardCollected).HasPrecision(18, 2);
        builder.Property(ws => ws.TotalTransferCollected).HasPrecision(18, 2);
        builder.Property(ws => ws.TotalDiscounts).HasPrecision(18, 2);
        builder.Property(ws => ws.ExpectedCash).HasPrecision(18, 2);
        builder.Property(ws => ws.ActualCashCounted).HasPrecision(18, 2);
        builder.Property(ws => ws.CashDifference).HasPrecision(18, 2);
        builder.Property(ws => ws.Notes).HasMaxLength(500);

        builder.Ignore(ws => ws.StartTime);
        builder.Ignore(ws => ws.EndTime);
        builder.Ignore(ws => ws.Duration);
        builder.Ignore(ws => ws.FormattedDuration);
        builder.Ignore(ws => ws.TotalRevenue);

        builder.HasIndex(ws => ws.UserId);
        builder.HasIndex(ws => ws.Status);
        builder.HasIndex(ws => ws.StartTimeUtc);
    }
}
