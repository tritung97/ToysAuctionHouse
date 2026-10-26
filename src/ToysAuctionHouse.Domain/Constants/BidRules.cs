namespace ToysAuctionHouse.Domain.Constants;

/// <summary>Centralised bid-rule constants that mirror the Lua hot-path logic.</summary>
public static class BidRules
{
    public const decimal MinimumStepPrice = 0.01m;
    public const decimal MinimumStartPrice = 0.01m;
    public const int MaxPrecisionScale = 2;
}
