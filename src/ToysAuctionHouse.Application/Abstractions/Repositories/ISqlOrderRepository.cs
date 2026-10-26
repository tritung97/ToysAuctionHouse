using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Application.Abstractions.Repositories;

public interface ISqlOrderRepository
{
    Task<IReadOnlyList<OrderDto>> GetByParticipantAsync(Guid userId, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Order> CreateAsync(Order order, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus status, Guid sellerId, CancellationToken ct = default);
}
