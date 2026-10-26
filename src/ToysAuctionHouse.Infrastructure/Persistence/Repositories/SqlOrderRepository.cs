using Microsoft.EntityFrameworkCore;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Infrastructure.Persistence;

namespace ToysAuctionHouse.Infrastructure.Persistence.Repositories;

public sealed class SqlOrderRepository : ISqlOrderRepository
{
    private readonly AuctionDbContext _db;

    public SqlOrderRepository(AuctionDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OrderDto>> GetByParticipantAsync(Guid userId, CancellationToken ct = default)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .Where(o => o.BuyerId == userId || o.SellerId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        return orders.Select(o => new OrderDto(
            o.Id,
            o.AuctionSessionId,
            o.BuyerId,
            o.Buyer.FullName,
            o.Buyer.PhoneNumber ?? string.Empty,
            o.SellerId,
            o.Seller.FullName,
            o.Seller.PhoneNumber ?? string.Empty,
            o.FinalAmount,
            (int)o.Status,
            o.Note,
            o.CreatedAt)).ToList();
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
        => _db.Orders.AsNoTracking()
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public async Task<Order> CreateAsync(Order order, CancellationToken ct = default)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return order;
    }

    public async Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus status, Guid sellerId, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct).ConfigureAwait(false);
        if (order is null || order.SellerId != sellerId || order.Status != OrderStatus.Pending)
        {
            return false;
        }

        order.Status = status;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
