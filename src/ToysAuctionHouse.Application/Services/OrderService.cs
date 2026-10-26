using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Application.Common;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Application.Services;

public class OrderService
{
    private readonly ISqlOrderRepository _orders;

    public OrderService(ISqlOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<IReadOnlyList<OrderDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => await _orders.GetByParticipantAsync(userId, ct).ConfigureAwait(false);

    public async Task<ServiceResult<OrderDto>> UpdateSettlementStatusAsync(Guid orderId, int targetStatus, Guid sellerId, CancellationToken ct = default)
    {
        var status = (OrderStatus)targetStatus;
        if (status is not (OrderStatus.Completed or OrderStatus.Cancelled))
        {
            return ServiceResult<OrderDto>.Fail(ServiceErrorCode.InvalidInput, "Invalid target status.");
        }

        var order = await _orders.GetByIdAsync(orderId, ct).ConfigureAwait(false);
        if (order is null)
        {
            return ServiceResult<OrderDto>.Fail(ServiceErrorCode.NotFound, "Order not found.");
        }

        if (order.SellerId != sellerId)
        {
            return ServiceResult<OrderDto>.Fail(ServiceErrorCode.Forbidden, "Only the seller can update this order.");
        }

        if (order.Status != OrderStatus.Pending)
        {
            return ServiceResult<OrderDto>.Fail(ServiceErrorCode.InvalidInput, "Only pending orders can be updated.");
        }

        var ok = await _orders.UpdateStatusAsync(orderId, status, sellerId, ct).ConfigureAwait(false);
        if (!ok)
        {
            return ServiceResult<OrderDto>.Fail(ServiceErrorCode.InvalidInput, "Failed to update order status.");
        }

        return ServiceResult<OrderDto>.Ok(new OrderDto(
            order.Id, order.AuctionSessionId, order.BuyerId, order.Buyer.FullName,
            order.Buyer.PhoneNumber ?? string.Empty, order.SellerId, order.Seller.FullName,
            order.Seller.PhoneNumber ?? string.Empty, order.FinalAmount, (int)status, order.Note, order.CreatedAt));
    }
}
