using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Domain.Entities;

namespace ToysAuctionHouse.Application.Abstractions.Repositories;

public interface ISqlProductRepository
{
    Task<PagedResultDto<ProductCardDto>> GetPagedAsync(CatalogFilterDto filter, CancellationToken ct = default);
    Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ToyProduct> CreateAsync(ToyProduct product, CancellationToken ct = default);
    Task InvalidateCacheAsync(Guid id, CancellationToken ct = default);
}
