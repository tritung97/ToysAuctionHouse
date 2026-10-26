using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t => t.HasCheckConstraint("CK_Orders_Status", "[Status] IN (1, 2, 3)"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.AuctionSessionId).IsUnique();

        builder.Property(x => x.FinalAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .HasDefaultValue(OrderStatus.Pending)
            .IsRequired();

        builder.Property(x => x.Note).HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
