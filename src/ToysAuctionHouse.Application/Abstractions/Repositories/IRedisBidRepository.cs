namespace ToysAuctionHouse.Application.Abstractions.Repositories;

public sealed record BidStreamMessage(
    string MessageId,
    Guid AuctionId,
    Guid BidderId,
    string BidId,
    decimal Amount,
    string IdempotencyKey,
    DateTime Timestamp);

/// <summary>The Redis hot-path bid port. All bid acceptance runs through one atomic Lua script.</summary>
public interface IRedisBidRepository
{
    Task<BidResult> PlaceBidAsync(
        Guid auctionId,
        Guid bidderId,
        string bidId,
        decimal amount,
        DateTime now,
        string idempotencyKey,
        CancellationToken ct = default);

    Task AddBidToStreamAsync(
        Guid auctionId,
        Guid bidderId,
        string bidId,
        decimal amount,
        DateTime timestamp,
        string idempotencyKey,
        CancellationToken ct = default);

    Task CreateBidsConsumerGroupAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BidStreamMessage>> ReadBidsAsync(int maxCount, CancellationToken ct = default);

    Task AckBidAsync(string messageId, CancellationToken ct = default);
}
