using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash. Null for OAuth-only accounts.</summary>
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>Nullable for OAuth accounts until the phone gate is satisfied.</summary>
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;

    /// <summary>OAuth subject identifier (e.g. Google `sub`). Null for local accounts.</summary>
    public string? ProviderKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ToyProduct> Products { get; set; } = new List<ToyProduct>();
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<Order> SellerOrders { get; set; } = new List<Order>();
    public ICollection<Order> BuyerOrders { get; set; } = new List<Order>();

    public bool HasPhoneNumber() => !string.IsNullOrWhiteSpace(PhoneNumber);
}
