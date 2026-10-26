using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ToysAuctionHouse.Application.Abstractions;

namespace ToysAuctionHouse.Api.Auth;

public sealed class JwtTokenService : IAuthTokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
    }

    public Task<GeneratedTokens> CreateTokensAsync(Guid userId, string username, CancellationToken ct = default)
    {
        var accessExpires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var accessToken = CreateToken(userId, username, "access", accessExpires);

        var refreshExpires = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        var refreshToken = CreateToken(userId, username, "refresh", refreshExpires);

        return Task.FromResult(new GeneratedTokens(accessToken, refreshToken, accessExpires));
    }

    public Task<(Guid UserId, string Username)?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var principal = ValidateToken(refreshToken);
        if (principal is null)
        {
            return Task.FromResult<(Guid, string)?>(null);
        }

        if (principal.FindFirst("token_type")?.Value != "refresh")
        {
            return Task.FromResult<(Guid, string)?>(null);
        }

        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = principal.FindFirst("username")?.Value;
        if (sub is null || username is null || !Guid.TryParse(sub, out var userId))
        {
            return Task.FromResult<(Guid, string)?>(null);
        }

        return Task.FromResult<(Guid, string)?>((userId, username));
    }

    private string CreateToken(Guid userId, string username, string tokenType, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("username", username),
            new("token_type", tokenType),
            new(ClaimTypes.Role, "Bidder"),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = _signingKey,
                NameClaimType = "username",
            };

            return handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }
}
