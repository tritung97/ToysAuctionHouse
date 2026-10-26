using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using ToysAuctionHouse.Application.Abstractions;

namespace ToysAuctionHouse.Api.Auth;

public sealed class GoogleOptions
{
    public const string SectionName = "Google";
    public string ClientId { get; set; } = string.Empty;
}

public sealed class GoogleTokenValidator
{
    private readonly GoogleOptions _options;

    public GoogleTokenValidator(IOptions<GoogleOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _options.ClientId },
        });

        return new GoogleIdentity(payload.Email, payload.Subject, payload.Name, payload.Picture);
    }
}
