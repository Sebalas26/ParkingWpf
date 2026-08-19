using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class PendingSyncItemConfiguration : IEntityTypeConfiguration<PendingSyncItem>
{
    public void Configure(EntityTypeBuilder<PendingSyncItem> builder)
    {
        builder.ToTable("PendingSyncItems");
        builder.HasKey(p => p.PendingSyncItemId);

        builder.Property(p => p.OperationType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.PayloadJson)
            .IsRequired();

        builder.Property(p => p.LastError)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();
    }
}
