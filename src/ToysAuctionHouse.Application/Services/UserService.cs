using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Application.Common;
using ToysAuctionHouse.Application.Dtos;

namespace ToysAuctionHouse.Application.Services;

public class UserService
{
    private readonly ISqlUserRepository _users;

    public UserService(ISqlUserRepository users)
    {
        _users = users;
    }

    public async Task<ServiceResult<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null)
        {
            return ServiceResult<UserProfileDto>.Fail(ServiceErrorCode.NotFound, "User not found.");
        }

        return ServiceResult<UserProfileDto>.Ok(new UserProfileDto(
            user.Id, user.Username, user.Email, user.FullName, user.PhoneNumber, user.AvatarUrl, (int)user.AuthProvider));
    }

    public async Task<ServiceResult<UserProfileDto>> UpdatePhoneAsync(Guid userId, string phoneNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return ServiceResult<UserProfileDto>.Fail(ServiceErrorCode.InvalidInput, "Phone number is required.");
        }

        await _users.UpdatePhoneAsync(userId, phoneNumber, ct).ConfigureAwait(false);
        return await GetProfileAsync(userId, ct).ConfigureAwait(false);
    }
}
