using System.Security.Claims;

namespace ToysAuctionHouse.Api.Auth;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid UserId
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string? Username
        => _accessor.HttpContext?.User.FindFirst("username")?.Value;

    public bool IsAuthenticated => UserId != Guid.Empty;
}
