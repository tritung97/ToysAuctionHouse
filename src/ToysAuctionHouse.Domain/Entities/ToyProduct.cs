using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Domain.Entities;

public class ToyProduct
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public User Seller { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Scale { get; set; }
    public string Brand { get; set; } = string.Empty;
    public ToyCondition Condition { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Serialized as a JSON array string (NVARCHAR(MAX)) by EF.</summary>
    public List<string> ImageUrls { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AuctionSession? AuctionSession { get; set; }
}
