using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Application.Common;
using ToysAuctionHouse.Application.Dtos;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Application.Services;

public class AuthService
{
    private readonly ISqlUserRepository _users;
    private readonly IAuthTokenService _tokens;
    private readonly IPasswordHasher _hasher;

    public AuthService(ISqlUserRepository users, IAuthTokenService tokens, IPasswordHasher hasher)
    {
        _users = users;
        _tokens = tokens;
        _hasher = hasher;
    }

    public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.FullName))
        {
            return ServiceResult<AuthResponseDto>.Fail(ServiceErrorCode.InvalidInput, "All fields are required.");
        }

        if (await _users.EmailExistsAsync(dto.Email, ct).ConfigureAwait(false))
        {
            return ServiceResult<AuthResponseDto>.Fail(ServiceErrorCode.Conflict, "Email already registered.");
        }

        if (await _users.UsernameExistsAsync(dto.Username, ct).ConfigureAwait(false))
        {
            return ServiceResult<AuthResponseDto>.Fail(ServiceErrorCode.Conflict, "Username already taken.");
        }

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            FullName = dto.FullName,
            PhoneNumber = dto.PhoneNumber,
            PasswordHash = _hasher.Hash(dto.Password),
            AuthProvider = AuthProvider.Local,
        };

        user = await _users.CreateAsync(user, ct).ConfigureAwait(false);

        var tokens = await _tokens.CreateTokensAsync(user.Id, user.Username, ct).ConfigureAwait(false);

        return ServiceResult<AuthResponseDto>.Ok(ToResponse(user, tokens));
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(dto.Email, ct).ConfigureAwait(false);
        if (user is null || user.PasswordHash is null || !_hasher.Verify(dto.Password, user.PasswordHash))
        {
            return ServiceResult<AuthResponseDto>.Fail(ServiceErrorCode.Unauthorized, "Invalid credentials.");
        }

        var tokens = await _tokens.CreateTokensAsync(user.Id, user.Username, ct).ConfigureAwait(false);
        return ServiceResult<AuthResponseDto>.Ok(ToResponse(user, tokens));
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginWithGoogleAsync(GoogleIdentity identity, CancellationToken ct = default)
    {
        var existing = await _users.GetByOAuthAsync(AuthProvider.Google, identity.Subject, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return ServiceResult<AuthResponseDto>.Ok(ToResponse(existing, await _tokens.CreateTokensAsync(existing.Id, existing.Username, ct).ConfigureAwait(false)));
        }

        var byEmail = await _users.GetByEmailAsync(identity.Email, ct).ConfigureAwait(false);
        if (byEmail is not null)
        {
            byEmail.AuthProvider = AuthProvider.Google;
            byEmail.ProviderKey = identity.Subject;
            byEmail.AvatarUrl ??= identity.Picture;

            var linked = await _users.UpdateAsync(byEmail, ct).ConfigureAwait(false);
            return ServiceResult<AuthResponseDto>.Ok(ToResponse(linked, await _tokens.CreateTokensAsync(linked.Id, linked.Username, ct).ConfigureAwait(false)));
        }

        var prefix = identity.Email.Split('@')[0];
        var username = $"{prefix}_{Random.Shared.Next(1000, 9999)}";
        while (await _users.UsernameExistsAsync(username, ct).ConfigureAwait(false))
        {
            username = $"{prefix}_{Random.Shared.Next(1000, 9999)}";
        }

        var newUser = new User
        {
            Username = username,
            Email = identity.Email,
            FullName = identity.Name,
            AvatarUrl = identity.Picture,
            AuthProvider = AuthProvider.Google,
            ProviderKey = identity.Subject,
            PasswordHash = null,
            PhoneNumber = null,
        };

        newUser = await _users.CreateAsync(newUser, ct).ConfigureAwait(false);

        var tokens = await _tokens.CreateTokensAsync(newUser.Id, newUser.Username, ct).ConfigureAwait(false);
        return ServiceResult<AuthResponseDto>.Ok(ToResponse(newUser, tokens));
    }

    public async Task<ServiceResult<AuthResponseDto>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var claim = await _tokens.ValidateRefreshTokenAsync(refreshToken, ct).ConfigureAwait(false);
        if (claim is null)
        {
            return ServiceResult<AuthResponseDto>.Fail(ServiceErrorCode.Unauthorized, "Invalid refresh token.");
        }

        var user = await _users.GetByIdAsync(claim.Value.UserId, ct).ConfigureAwait(false);
        if (user is null)
        {
            return ServiceResult<AuthResponseDto>.Fail(ServiceErrorCode.Unauthorized, "User not found.");
        }

        var tokens = await _tokens.CreateTokensAsync(user.Id, user.Username, ct).ConfigureAwait(false);
        return ServiceResult<AuthResponseDto>.Ok(ToResponse(user, tokens));
    }

    private static AuthResponseDto ToResponse(User user, GeneratedTokens tokens)
        => new(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresAt, user.Id, user.Username);
}
