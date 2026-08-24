using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Entities;

namespace Parking.Data.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(s => s.SessionId);

        builder.Property(s => s.SessionToken).IsRequired().HasMaxLength(100);
        builder.HasIndex(s => s.SessionToken).IsUnique();

        builder.Property(s => s.DeviceIdentifier).IsRequired().HasMaxLength(150);
        builder.Property(s => s.IpAddress).HasMaxLength(45);

        builder.HasIndex(s => new { s.UserId, s.IsActive });

        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
