using Microsoft.AspNetCore.Mvc;
using ToysAuctionHouse.Api.Auth;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Application.Services;

namespace ToysAuctionHouse.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly GoogleTokenValidator _google;

    public AuthController(AuthService auth, GoogleTokenValidator google)
    {
        _auth = auth;
        _google = google;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
        => ApiResults.ToActionResult(await _auth.RegisterAsync(dto, ct).ConfigureAwait(false));

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
        => ApiResults.ToActionResult(await _auth.LoginAsync(dto, ct).ConfigureAwait(false));

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] OAuthDto dto, CancellationToken ct)
    {
        var identity = await _google.ValidateAsync(dto.IdToken, ct).ConfigureAwait(false);
        return ApiResults.ToActionResult(await _auth.LoginWithGoogleAsync(identity, ct).ConfigureAwait(false));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshDto dto, CancellationToken ct)
        => ApiResults.ToActionResult(await _auth.RefreshAsync(dto.RefreshToken, ct).ConfigureAwait(false));
}

public sealed record RefreshDto(string RefreshToken);
