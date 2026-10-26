namespace ToysAuctionHouse.Infrastructure.Redis;

/// <summary>
/// Canonical Redis key schema for the auction hot store. Hand-rolled (no string
/// interpolation drift) so callers and the Lua script always agree.
/// </summary>
public static class AuctionKeyBuilder
{
    public const string ActiveListKey = "auction:active_list";
    public const string BidsStreamKey = "auction:bids:stream";
    public const string BidsConsumerGroup = "AuctionBidPersistors";
    public const string ConsumerName = "persistor-1";
    public const string IdempotencyPrefix = "idempotency:";
    public const string RoomGroupPrefix = "Auction_";

    public static string Meta(Guid auctionId) => $"auction:{auctionId}:meta";
    public static string Bids(Guid auctionId) => $"auction:{auctionId}:bids";
    public static string Idempotency(string key) => $"{IdempotencyPrefix}{key}";
    public static string RoomGroup(Guid auctionId) => $"{RoomGroupPrefix}{auctionId}";
}
