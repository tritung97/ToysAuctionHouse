namespace ToysAuctionHouse.Application.Abstractions;

public sealed record GoogleIdentity(string Email, string Subject, string Name, string? Picture);

public sealed record GeneratedTokens(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IAuthTokenService
{
    Task<GeneratedTokens> CreateTokensAsync(Guid userId, string username, CancellationToken ct = default);

    /// <summary>Validates a refresh token and returns the associated (userId, username).</summary>
    Task<(Guid UserId, string Username)?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}
