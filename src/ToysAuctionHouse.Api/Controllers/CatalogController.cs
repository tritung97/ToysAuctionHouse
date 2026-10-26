using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToysAuctionHouse.Api.Auth;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Application.Services;

namespace ToysAuctionHouse.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly CatalogService _catalog;

    public CatalogController(CatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] CatalogFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResultDto<ProductCardDto>>.Ok(await _catalog.BrowseAsync(filter, ct).ConfigureAwait(false)));

    [HttpGet("/api/auctions/{id:guid}")]
    public async Task<IActionResult> RoomDetail(Guid id, CancellationToken ct)
        => ApiResults.ToActionResult(await _catalog.GetRoomDetailAsync(id, ct).ConfigureAwait(false));
}
