namespace ToysAuctionHouse.Application.Dtos;

public sealed record RegisterDto(string Username, string Email, string Password, string FullName, string? PhoneNumber);

public sealed record LoginDto(string Email, string Password);

public sealed record OAuthDto(string IdToken);

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string Username);

public sealed record UserProfileDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    int AuthProvider);
