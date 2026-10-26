using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Application.Abstractions.Repositories;

public interface ISqlAuctionRepository
{
    Task<AuctionSession?> GetByIdAsync(Guid auctionId, CancellationToken ct = default);

    Task<IReadOnlyList<AuctionSession>> GetScheduledDueAsync(DateTime now, CancellationToken ct = default);

    Task<AuctionSession?> GetByProductIdAsync(Guid productId, CancellationToken ct = default);

    Task<AuctionSession> CreateAsync(AuctionSession session, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid auctionId, AuctionSessionStatus status, CancellationToken ct = default);

    /// <summary>Idempotently persists accepted bids (skips any with an already-present IdempotencyKey).</summary>
    Task PersistBidsAsync(IReadOnlyList<BidPersistItem> bids, CancellationToken ct = default);

    /// <summary>Atomically finalizes: updates session, bulk-inserts bids (idempotent), creates an order. Returns the order id (null when no winner).</summary>
    Task<Guid?> FinalizeAndPersistAsync(FinalizeCommand command, CancellationToken ct = default);
}
