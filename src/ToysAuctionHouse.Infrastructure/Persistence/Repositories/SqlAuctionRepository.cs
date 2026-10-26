using Microsoft.EntityFrameworkCore;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Infrastructure.Persistence;

namespace ToysAuctionHouse.Infrastructure.Persistence.Repositories;

public sealed class SqlAuctionRepository : ISqlAuctionRepository
{
    private readonly AuctionDbContext _db;

    public SqlAuctionRepository(AuctionDbContext db)
    {
        _db = db;
    }

    public Task<AuctionSession?> GetByIdAsync(Guid auctionId, CancellationToken ct = default)
        => _db.AuctionSessions.AsNoTracking()
            .Include(s => s.Product)
            .ThenInclude(p => p.Seller)
            .FirstOrDefaultAsync(s => s.Id == auctionId, ct);

    public async Task<IReadOnlyList<AuctionSession>> GetScheduledDueAsync(DateTime now, CancellationToken ct = default)
    {
        var result = await _db.AuctionSessions.AsNoTracking()
            .Include(s => s.Product)
            .Where(s => s.Status == AuctionSessionStatus.Scheduled && s.StartTime <= now)
            .Take(200)
            .ToListAsync(ct).ConfigureAwait(false);

        return result;
    }

    public Task<AuctionSession?> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => _db.AuctionSessions.AsNoTracking()
            .Include(s => s.Product)
            .ThenInclude(p => p.Seller)
            .FirstOrDefaultAsync(s => s.ProductId == productId, ct);

    public async Task<AuctionSession> CreateAsync(AuctionSession session, CancellationToken ct = default)
    {
        _db.AuctionSessions.Add(session);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return session;
    }

    public async Task UpdateStatusAsync(Guid auctionId, AuctionSessionStatus status, CancellationToken ct = default)
    {
        var session = await _db.AuctionSessions.FirstOrDefaultAsync(s => s.Id == auctionId, ct).ConfigureAwait(false);
        if (session is not null)
        {
            session.Status = status;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task PersistBidsAsync(IReadOnlyList<BidPersistItem> bids, CancellationToken ct = default)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var bid in bids)
        {
            if (!seen.Add(bid.IdempotencyKey))
            {
                continue;
            }

            var exists = await _db.Bids.AsNoTracking()
                .AnyAsync(b => b.IdempotencyKey == bid.IdempotencyKey, ct)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            _db.Bids.Add(new Bid
            {
                AuctionSessionId = bid.AuctionId,
                BidderId = bid.BidderId,
                Amount = bid.Amount,
                IdempotencyKey = bid.IdempotencyKey,
                CreatedAt = bid.CreatedAt,
            });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<Guid?> FinalizeAndPersistAsync(FinalizeCommand command, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var session = await _db.AuctionSessions
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == command.AuctionId, ct)
            .ConfigureAwait(false);

        if (session is null)
        {
            throw new InvalidOperationException($"AuctionSession {command.AuctionId} not found during finalization.");
        }

        session.Finalize(command.WinnerId, command.FinalAmount);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var bid in command.Bids)
        {
            var exists = await _db.Bids.AsNoTracking()
                .AnyAsync(b => b.IdempotencyKey == bid.IdempotencyKey, ct)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            _db.Bids.Add(new Bid
            {
                AuctionSessionId = command.AuctionId,
                BidderId = bid.BidderId,
                Amount = bid.Amount,
                IdempotencyKey = bid.IdempotencyKey,
                CreatedAt = bid.CreatedAt,
            });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        Guid? orderId = null;
        if (command.WinnerId.HasValue)
        {
            var order = new Order
            {
                AuctionSessionId = command.AuctionId,
                BuyerId = command.WinnerId.Value,
                SellerId = session.Product.SellerId,
                FinalAmount = command.FinalAmount,
                Status = OrderStatus.Pending,
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            orderId = order.Id;
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return orderId;
    }
}
