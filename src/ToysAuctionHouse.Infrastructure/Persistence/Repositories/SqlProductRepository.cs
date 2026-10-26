using Microsoft.EntityFrameworkCore;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Infrastructure.Caching;
using ToysAuctionHouse.Infrastructure.Persistence;

namespace ToysAuctionHouse.Infrastructure.Persistence.Repositories;

public sealed class SqlProductRepository : ISqlProductRepository
{
    private static readonly string ProductPrefix = "catalog:product:";

    private readonly AuctionDbContext _db;
    private readonly CacheAsideService _cache;

    public SqlProductRepository(AuctionDbContext db, CacheAsideService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PagedResultDto<ProductCardDto>> GetPagedAsync(CatalogFilterDto filter, CancellationToken ct = default)
    {
        var query = _db.ToyProducts.AsNoTracking()
            .Where(p => p.AuctionSession != null)
            .Select(p => new { Product = p, Session = p.AuctionSession! });

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var cat = filter.Category.Trim();
            query = query.Where(x => x.Product.Category == cat);
        }

        if (filter.Condition.HasValue)
        {
            var cond = (ToyCondition)filter.Condition.Value;
            query = query.Where(x => x.Product.Condition == cond);
        }

        if (filter.Status.HasValue)
        {
            var status = (AuctionSessionStatus)filter.Status.Value;
            query = query.Where(x => x.Session.Status == status);
        }

        query = query.OrderByDescending(x => x.Product.CreatedAt);

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var page = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductCardDto(
                x.Product.Id,
                x.Product.Name,
                x.Product.Category,
                x.Product.Brand,
                (int)x.Product.Condition,
                x.Session.CurrentPrice,
                x.Session.EndTime,
                (int)x.Session.Status,
                x.Product.ImageUrls.FirstOrDefault()))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResultDto<ProductCardDto>(items, total, page, pageSize);
    }

    public async Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var key = $"{ProductPrefix}{id}";

        var cached = await _cache.GetAsync<ProductDetailDto>(key, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var result = await _db.ToyProducts.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetailDto(
                p.Id,
                p.SellerId,
                p.Seller.FullName,
                p.Seller.PhoneNumber ?? string.Empty,
                p.Name,
                p.Category,
                p.Scale,
                p.Brand,
                (int)p.Condition,
                p.Description,
                p.ImageUrls,
                p.AuctionSession == null ? null : (int?)p.AuctionSession.Status,
                p.AuctionSession == null ? null : (DateTime?)p.AuctionSession.EndTime))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (result is not null)
        {
            await _cache.SetAsync(key, result, TimeSpan.FromMinutes(10), ct).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<ToyProduct> CreateAsync(ToyProduct product, CancellationToken ct = default)
    {
        _db.ToyProducts.Add(product);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return product;
    }

    public Task InvalidateCacheAsync(Guid id, CancellationToken ct = default)
        => _cache.RemoveAsync($"{ProductPrefix}{id}", ct);
}
