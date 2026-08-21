using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class CashWithdrawalConfiguration : IEntityTypeConfiguration<CashWithdrawal>
{
    public void Configure(EntityTypeBuilder<CashWithdrawal> builder)
    {
        builder.ToTable("CashWithdrawals");
        builder.HasKey(cw => cw.WithdrawalId);

        builder.Property(cw => cw.Amount).HasPrecision(18, 2);
        builder.Property(cw => cw.Reason).HasMaxLength(250);
        builder.Property(cw => cw.AuthorizedByAdminName).HasMaxLength(100);
        builder.Property(cw => cw.CashierName).HasMaxLength(100);

        builder.HasIndex(cw => cw.ShiftId);
        builder.HasIndex(cw => cw.CreatedAtUtc);
    }
}
