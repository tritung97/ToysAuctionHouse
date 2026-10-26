using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToysAuctionHouse.Domain.Entities;

namespace ToysAuctionHouse.Infrastructure.Persistence.Configurations;

public class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("Bids");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.IdempotencyKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.AuctionSessionId, x.Amount })
            .HasDatabaseName("IX_Bids_AuctionSessionId_Amount")
            .IsDescending(false, true);
    }
}
