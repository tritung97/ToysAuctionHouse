using ToysAuctionHouse.Domain.Entities;

namespace ToysAuctionHouse.Application.Abstractions.Repositories;

/// <summary>Redis auction room-state port (meta hash, ZSET, active-list, lifecycle).</summary>
public interface IRedisAuctionRepository
{
    Task InitSessionAsync(AuctionSession session, CancellationToken ct = default);

    Task<AuctionSnapshot?> GetMetaAsync(Guid auctionId, CancellationToken ct = default);

    Task<IReadOnlyList<BidEntry>> GetTopBidsAsync(Guid auctionId, int count, CancellationToken ct = default);

    Task<BidEntry?> GetWinningBidAsync(Guid auctionId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetExpiredActiveAsync(DateTime now, CancellationToken ct = default);

    Task MarkEndedAsync(Guid auctionId, CancellationToken ct = default);

    Task RemoveFromActiveListAsync(Guid auctionId, CancellationToken ct = default);

    Task CleanupAsync(Guid auctionId, CancellationToken ct = default);
}
