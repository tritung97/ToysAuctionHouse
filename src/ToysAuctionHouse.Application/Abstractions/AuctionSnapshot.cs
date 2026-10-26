namespace ToysAuctionHouse.Application.Abstractions;

/// <summary>Read-model of the Redis auction meta hash.</summary>
public sealed record AuctionSnapshot(
    Guid AuctionId,
    string Status,
    decimal StartPrice,
    decimal StepPrice,
    decimal CurrentPrice,
    Guid? CurrentWinnerId,
    DateTime StartTime,
    DateTime EndTime,
    Guid SellerId,
    Guid ProductId,
    bool IsAntiSnipingEnabled);

public sealed record BidEntry(decimal Amount, Guid BidderId, long TimestampMs, string BidId);
