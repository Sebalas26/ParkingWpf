using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class TicketDiscountConfiguration : IEntityTypeConfiguration<TicketDiscount>
{
    public void Configure(EntityTypeBuilder<TicketDiscount> builder)
    {
        builder.ToTable("TicketDiscounts");
        builder.HasKey(td => td.TicketDiscountId);

        builder.Property(td => td.InvoiceNumber).IsRequired().HasMaxLength(60);
        builder.Property(td => td.PurchaseAmount).HasPrecision(18, 2);
        builder.Property(td => td.AppliedDiscountAmount).HasPrecision(18, 2);

        builder.HasOne(td => td.Ticket)
            .WithMany(t => t.Discounts)
            .HasForeignKey(td => td.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(td => td.Store)
            .WithMany(s => s.TicketDiscounts)
            .HasForeignKey(td => td.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(td => td.Agreement)
            .WithMany(a => a.TicketDiscounts)
            .HasForeignKey(td => td.AgreementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
