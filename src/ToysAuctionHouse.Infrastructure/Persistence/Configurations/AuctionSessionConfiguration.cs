using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Infrastructure.Persistence.Configurations;

public class AuctionSessionConfiguration : IEntityTypeConfiguration<AuctionSession>
{
    public void Configure(EntityTypeBuilder<AuctionSession> builder)
    {
        builder.ToTable("AuctionSessions", t => t.HasCheckConstraint("CK_AuctionSessions_Status", "[Status] IN (1, 2, 3, 4)"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.ProductId).IsUnique();

        builder.Property(x => x.StartPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.StepPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CurrentPrice).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.StartTime).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.EndTime).HasColumnType("datetime2").IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .HasDefaultValue(AuctionSessionStatus.Scheduled)
            .IsRequired();

        builder.Property(x => x.IsAntiSnipingEnabled)
            .HasColumnType("bit")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.Status, x.EndTime })
            .HasDatabaseName("IX_AuctionSessions_Status_EndTime")
            .IncludeProperties(x => new { x.CurrentPrice, x.WinnerId });

        builder.HasOne(x => x.Winner)
            .WithMany()
            .HasForeignKey(x => x.WinnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Bids)
            .WithOne(b => b.AuctionSession)
            .HasForeignKey(b => b.AuctionSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Order)
            .WithOne(o => o.AuctionSession)
            .HasForeignKey<Order>(o => o.AuctionSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
