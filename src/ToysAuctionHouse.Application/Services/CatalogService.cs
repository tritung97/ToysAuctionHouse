using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Application.Common;
using ToysAuctionHouse.Application.Dtos;

namespace ToysAuctionHouse.Application.Services;

public class CatalogService
{
    private readonly ISqlProductRepository _products;
    private readonly ISqlAuctionRepository _auctions;
    private readonly ISqlUserRepository _users;
    private readonly IRedisAuctionRepository _redisAuctions;

    public CatalogService(
        ISqlProductRepository products,
        ISqlAuctionRepository auctions,
        ISqlUserRepository users,
        IRedisAuctionRepository redisAuctions)
    {
        _products = products;
        _auctions = auctions;
        _users = users;
        _redisAuctions = redisAuctions;
    }

    public Task<PagedResultDto<ProductCardDto>> BrowseAsync(CatalogFilterDto filter, CancellationToken ct = default)
        => _products.GetPagedAsync(filter, ct);

    public async Task<ServiceResult<AuctionRoomDto>> GetRoomDetailAsync(Guid auctionId, CancellationToken ct = default)
    {
        var meta = await _redisAuctions.GetMetaAsync(auctionId, ct).ConfigureAwait(false);

        if (meta is not null)
        {
            var topBids = await _redisAuctions.GetTopBidsAsync(auctionId, 10, ct).ConfigureAwait(false);
            var product = await _products.GetByIdAsync(meta.ProductId, ct).ConfigureAwait(false);

            if (product is null)
            {
                return ServiceResult<AuctionRoomDto>.Fail(ServiceErrorCode.NotFound, "Auction not found.");
            }

            var bidderNames = await LoadBidderNamesAsync(topBids.Select(b => b.BidderId), ct).ConfigureAwait(false);

            var bidDtos = topBids.Select(b => new BidDto(
                b.BidderId,
                bidderNames.GetValueOrDefault(b.BidderId, $"bidder_{b.BidderId}"),
                b.Amount,
                DateTimeOffset.FromUnixTimeMilliseconds(b.TimestampMs).UtcDateTime)).ToList();

            return ServiceResult<AuctionRoomDto>.Ok(new AuctionRoomDto(
                auctionId,
                product.Id,
                product.Name,
                product.Category,
                product.Brand,
                product.Scale,
                product.Condition,
                product.Description,
                product.ImageUrls,
                product.SellerId,
                product.SellerName,
                product.SellerPhone,
                meta.StartPrice,
                meta.StepPrice,
                meta.CurrentPrice,
                meta.StartTime,
                meta.EndTime,
                meta.Status,
                meta.IsAntiSnipingEnabled,
                bidDtos));
        }

        var auction = await _auctions.GetByIdAsync(auctionId, ct).ConfigureAwait(false);
        if (auction is null || auction.Product is null)
        {
            return ServiceResult<AuctionRoomDto>.Fail(ServiceErrorCode.NotFound, "Auction not found.");
        }

        return ServiceResult<AuctionRoomDto>.Ok(new AuctionRoomDto(
            auction.Id,
            auction.Product.Id,
            auction.Product.Name,
            auction.Product.Category,
            auction.Product.Brand,
            auction.Product.Scale,
            (int)auction.Product.Condition,
            auction.Product.Description,
            auction.Product.ImageUrls,
            auction.Product.SellerId,
            auction.Product.Seller.FullName,
            auction.Product.Seller.PhoneNumber ?? string.Empty,
            auction.StartPrice,
            auction.StepPrice,
            auction.CurrentPrice,
            auction.StartTime,
            auction.EndTime,
            ((int)auction.Status).ToString(),
            auction.IsAntiSnipingEnabled,
            new List<BidDto>()));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadBidderNamesAsync(IEnumerable<Guid> bidderIds, CancellationToken ct)
    {
        var users = await _users.GetByIdsAsync(bidderIds, ct).ConfigureAwait(false);
        return users.ToDictionary(x => x.Key, x => x.Value.FullName);
    }
}
