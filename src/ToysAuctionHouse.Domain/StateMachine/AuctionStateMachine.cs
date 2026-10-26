using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Domain.StateMachine;

/// <summary>Outcome of validating a bid against auction+amount rules.</summary>
public enum BidDecision
{
    Accepted,
    Duplicate,
    InvalidAmount,
    AuctionEnded,
    AuctionNotStarted,
}

/// <summary>
/// Pure, side-effect-free state transition rules for an auction session.
/// These mirror the Redis Lua hot-path logic one-for-one (single source of
/// truth that a fidelity unit test can assert against the Lua string).
/// </summary>
public static class AuctionStateMachine
{
    public static bool CanActivate(AuctionSessionStatus status) => status == AuctionSessionStatus.Scheduled;

    public static bool CanCancel(AuctionSessionStatus status) => status == AuctionSessionStatus.Scheduled;

    public static bool CanBid(AuctionSessionStatus status) => status == AuctionSessionStatus.Active;

    public static bool CanFinalize(AuctionSessionStatus status) => status == AuctionSessionStatus.Active;

    /// <summary>
    /// Evaluates whether a bid can be accepted. Duplicate detection is handled
    /// out-of-band (Redis SETNX / SQL unique index), so this method does not
    /// decide about duplicates.
    /// </summary>
    public static BidDecision ValidateBid(
        AuctionSessionStatus status,
        DateTime now,
        DateTime startTime,
        DateTime endTime,
        decimal amount,
        decimal stepPrice,
        decimal currentPrice)
    {
        if (status != AuctionSessionStatus.Active)
        {
            return BidDecision.AuctionEnded;
        }

        if (now < startTime)
        {
            return BidDecision.AuctionNotStarted;
        }

        if (now >= endTime)
        {
            return BidDecision.AuctionEnded;
        }

        if (stepPrice <= 0)
        {
            return BidDecision.InvalidAmount;
        }

        if (amount < currentPrice + stepPrice || amount <= currentPrice)
        {
            return BidDecision.InvalidAmount;
        }

        return BidDecision.Accepted;
    }

    /// <summary>
    /// Returns a new end time when anti-sniping should extend the auction,
    /// otherwise null. Mirrors the Lua extension branch.
    /// </summary>
    public static DateTime? ApplyAntiSniping(
        DateTime endTime,
        DateTime now,
        bool antiSnipingEnabled,
        int snipeWindowSeconds,
        int extensionSeconds)
    {
        if (!antiSnipingEnabled)
        {
            return null;
        }

        var remaining = endTime - now;
        if (remaining > TimeSpan.Zero && remaining <= TimeSpan.FromSeconds(snipeWindowSeconds))
        {
            return now.AddSeconds(extensionSeconds);
        }

        return null;
    }
}
