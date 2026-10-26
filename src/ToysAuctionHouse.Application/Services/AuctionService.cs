using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Application.Common;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Application.Services;

public class AuctionService
{
    private readonly ISqlAuctionRepository _auctions;
    private readonly ISqlProductRepository _products;
    private readonly ISqlUserRepository _users;
    private readonly IRedisBidRepository _redisBids;

    public AuctionService(
        ISqlAuctionRepository auctions,
        ISqlProductRepository products,
        ISqlUserRepository users,
        IRedisBidRepository redisBids)
    {
        _auctions = auctions;
        _products = products;
        _users = users;
        _redisBids = redisBids;
    }

    public async Task<ServiceResult<Guid>> CreateAsync(Guid sellerId, CreateAuctionRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var auction = request.Auction;

        if (auction.StartTime < now)
        {
            return ServiceResult<Guid>.Fail(ServiceErrorCode.InvalidInput, "StartTime cannot be in the past.");
        }

        if (auction.EndTime <= auction.StartTime)
        {
            return ServiceResult<Guid>.Fail(ServiceErrorCode.InvalidInput, "EndTime must be after StartTime.");
        }

        if (auction.StartPrice <= 0 || auction.StepPrice <= 0)
        {
            return ServiceResult<Guid>.Fail(ServiceErrorCode.InvalidInput, "StartPrice and StepPrice must be positive.");
        }

        var phone = await EnsurePhoneAsync(sellerId, ct).ConfigureAwait(false);
        if (!phone)
        {
            return ServiceResult<Guid>.Fail(ServiceErrorCode.PhoneRequired, "A phone number is required to list an auction.");
        }

        var product = new ToyProduct
        {
            SellerId = sellerId,
            Name = request.Product.Name,
            Category = request.Product.Category,
            Scale = request.Product.Scale,
            Brand = request.Product.Brand,
            Condition = (ToyCondition)request.Product.Condition,
            Description = request.Product.Description,
            ImageUrls = request.Product.ImageUrls.ToList(),
        };

        product = await _products.CreateAsync(product, ct).ConfigureAwait(false);

        var session = new AuctionSession
        {
            ProductId = product.Id,
            StartPrice = auction.StartPrice,
            StepPrice = auction.StepPrice,
            CurrentPrice = auction.StartPrice,
            StartTime = auction.StartTime,
            EndTime = auction.EndTime,
            Status = AuctionSessionStatus.Scheduled,
            IsAntiSnipingEnabled = auction.IsAntiSnipingEnabled,
        };

        session = await _auctions.CreateAsync(session, ct).ConfigureAwait(false);
        await _products.InvalidateCacheAsync(product.Id, ct).ConfigureAwait(false);

        return ServiceResult<Guid>.Ok(session.Id);
    }

    public async Task<ServiceResult> CancelAsync(Guid sellerId, Guid auctionId, CancellationToken ct = default)
    {
        var session = await _auctions.GetByIdAsync(auctionId, ct).ConfigureAwait(false);
        if (session is null)
        {
            return ServiceResult.Ok;
        }

        if (!session.CancelBy(sellerId))
        {
            return ServiceResult.Fail(ServiceErrorCode.CannotCancel, "Cannot cancel an ongoing or completed auction.");
        }

        await _auctions.UpdateStatusAsync(auctionId, AuctionSessionStatus.Cancelled, ct).ConfigureAwait(false);
        await _products.InvalidateCacheAsync(session.ProductId, ct).ConfigureAwait(false);

        return ServiceResult.Ok;
    }

    public async Task<ServiceResult<BidResultDto>> PlaceBidAsync(Guid bidderId, Guid auctionId, decimal amount, string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ServiceResult<BidResultDto>.Fail(ServiceErrorCode.InvalidInput, "Idempotency key is required.");
        }

        var phone = await EnsurePhoneAsync(bidderId, ct).ConfigureAwait(false);
        if (!phone)
        {
            return ServiceResult<BidResultDto>.Fail(ServiceErrorCode.PhoneRequired, "A phone number is required to place a bid.");
        }

        var bidId = Guid.NewGuid().ToString("N");
        var result = await _redisBids.PlaceBidAsync(
            auctionId, bidderId, bidId, amount, DateTime.UtcNow, idempotencyKey, ct).ConfigureAwait(false);

        var dto = new BidResultDto(
            result.Status.ToString(),
            result.CurrentPrice,
            result.WinnerId,
            result.Extended,
            result.NewEndTime,
            StatusMessage(result.Status));

        return ServiceResult<BidResultDto>.Ok(dto);
    }

    private async Task<bool> EnsurePhoneAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        return user?.HasPhoneNumber() == true;
    }

    private static string? StatusMessage(BidStatus status)
        => status switch
        {
            BidStatus.Duplicate => "Duplicate bid request.",
            BidStatus.Outbid => "Outbid by another user! Please refresh bid amount.",
            BidStatus.Ended => "Auction session has ended.",
            BidStatus.NotStarted => "Auction has not started yet.",
            _ => null,
        };
}
