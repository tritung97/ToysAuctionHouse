using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid AuctionSessionId { get; set; }
    public AuctionSession AuctionSession { get; set; } = null!;
    public Guid BuyerId { get; set; }
    public User Buyer { get; set; } = null!;
    public Guid SellerId { get; set; }
    public User Seller { get; set; } = null!;
    public decimal FinalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
