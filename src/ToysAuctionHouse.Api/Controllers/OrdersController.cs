using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToysAuctionHouse.Api.Auth;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Application.Services;

namespace ToysAuctionHouse.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orders;
    private readonly CurrentUserService _current;

    public OrdersController(OrderService orders, CurrentUserService current)
    {
        _orders = orders;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var orders = await _orders.ListForUserAsync(_current.UserId, ct).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<OrderDto>>.Ok(orders));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusDto dto, CancellationToken ct)
        => ApiResults.ToActionResult(await _orders.UpdateSettlementStatusAsync(id, dto.Status, _current.UserId, ct).ConfigureAwait(false));
}

public sealed record UpdateOrderStatusDto(int Status);
