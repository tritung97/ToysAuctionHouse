using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Infrastructure.Persistence.Configurations;

public class ToyProductConfiguration : IEntityTypeConfiguration<ToyProduct>
{
    private static readonly ValueConverter<List<string>, string> ImageUrlConverter =
        new(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    private static readonly ValueComparer<List<string>> ImageUrlComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        v => v.ToList());

    public void Configure(EntityTypeBuilder<ToyProduct> builder)
    {
        builder.ToTable("ToyProducts", t => t.HasCheckConstraint("CK_ToyProducts_Condition", "[Condition] IN (1, 2, 3)"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Scale).HasMaxLength(20);
        builder.Property(x => x.Brand).HasMaxLength(100).IsRequired();

        builder.Property(x => x.Condition)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(x => x.Description).IsRequired();

        builder.Property(x => x.ImageUrls)
            .HasConversion(ImageUrlConverter)
            .Metadata.SetValueComparer(ImageUrlComparer);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.Category, x.Condition })
            .HasDatabaseName("IX_ToyProducts_Category_Condition");

        builder.HasOne(x => x.AuctionSession)
            .WithOne(s => s.Product)
            .HasForeignKey<AuctionSession>(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
