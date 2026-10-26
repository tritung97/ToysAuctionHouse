namespace ToysAuctionHouse.Application.Abstractions;

public enum BidStatus
{
    Accepted,
    Duplicate,
    Outbid,
    Ended,
    NotStarted,
    NotFound,
}

public sealed record BidResult(
    BidStatus Status,
    decimal CurrentPrice,
    Guid? WinnerId,
    bool Extended,
    DateTime? NewEndTime);
