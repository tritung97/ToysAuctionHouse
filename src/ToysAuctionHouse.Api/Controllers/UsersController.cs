using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToysAuctionHouse.Api.Auth;
using ToysAuctionHouse.Application.Services;

namespace ToysAuctionHouse.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _users;
    private readonly CurrentUserService _current;

    public UsersController(UserService users, CurrentUserService current)
    {
        _users = users;
        _current = current;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => ApiResults.ToActionResult(await _users.GetProfileAsync(_current.UserId, ct).ConfigureAwait(false));

    [HttpPut("me/phone")]
    public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneDto dto, CancellationToken ct)
        => ApiResults.ToActionResult(await _users.UpdatePhoneAsync(_current.UserId, dto.PhoneNumber, ct).ConfigureAwait(false));
}

public sealed record UpdatePhoneDto(string PhoneNumber);
