namespace ToysAuctionHouse.Application.Abstractions;

public sealed record BidPersistItem(Guid AuctionId, Guid BidderId, decimal Amount, string IdempotencyKey, DateTime CreatedAt);

/// <summary>Everything the finalizer needs to persist one auction end in a single SQL transaction.</summary>
public sealed record FinalizeCommand(
    Guid AuctionId,
    Guid? WinnerId,
    decimal FinalAmount,
    IReadOnlyList<BidPersistItem> Bids);
