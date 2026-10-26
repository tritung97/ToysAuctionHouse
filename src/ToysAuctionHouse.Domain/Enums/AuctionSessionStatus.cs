namespace ToysAuctionHouse.Domain.Enums;

/// <summary>Lifecycle state of an auction session. Values match DB CHECK constraint.</summary>
public enum AuctionSessionStatus : byte
{
    Scheduled = 1,
    Active = 2,
    Ended = 3,
    Cancelled = 4,
}
