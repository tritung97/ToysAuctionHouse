namespace ToysAuctionHouse.Domain.Entities;

public class Bid
{
    public Guid Id { get; set; }
    public Guid AuctionSessionId { get; set; }
    public AuctionSession AuctionSession { get; set; } = null!;
    public Guid BidderId { get; set; }
    public User Bidder { get; set; } = null!;
    public decimal Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
