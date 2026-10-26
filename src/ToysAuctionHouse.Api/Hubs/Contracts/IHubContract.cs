namespace ToysAuctionHouse.Api.Hubs.Contracts;

/// <summary>Canonical SignalR client method names. Kept in one place so the Hub and notifier agree.</summary>
public static class HubContract
{
    public const string RoomSnapshot = "RoomSnapshot";
    public const string ReceiveNewBid = "ReceiveNewBid";
    public const string AuctionExtended = "AuctionExtended";
    public const string AuctionStarted = "AuctionStarted";
    public const string AuctionEnded = "AuctionEnded";
    public const string BidAccepted = "BidAccepted";
    public const string BidRejected = "BidRejected";
}
