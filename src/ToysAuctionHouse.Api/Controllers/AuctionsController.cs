using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToysAuctionHouse.Api.Auth;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Application.Services;

namespace ToysAuctionHouse.Api.Controllers;

[ApiController]
[Route("api/auctions")]
public class AuctionsController : ControllerBase
{
    private readonly AuctionService _auctions;
    private readonly CurrentUserService _current;

    public AuctionsController(AuctionService auctions, CurrentUserService current)
    {
        _auctions = auctions;
        _current = current;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateAuctionRequest request, CancellationToken ct)
        => ApiResults.ToActionResult(await _auctions.CreateAsync(_current.UserId, request, ct).ConfigureAwait(false));

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ApiResults.ToActionResult(await _auctions.CancelAsync(_current.UserId, id, ct).ConfigureAwait(false));
}
