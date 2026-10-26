using ToysAuctionHouse.Domain.Constants;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Domain.StateMachine;

namespace ToysAuctionHouse.Domain.Entities;

/// <summary>Result of a bid attempt evaluated against the domain state machine.</summary>
public sealed record BidAcceptance(bool Accepted, BidDecision Decision, bool Extended, DateTime? NewEndTime);

public class AuctionSession
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public ToyProduct Product { get; set; } = null!;

    public decimal StartPrice { get; set; }
    public decimal StepPrice { get; set; }
    public decimal CurrentPrice { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public AuctionSessionStatus Status { get; set; } = AuctionSessionStatus.Scheduled;
    public bool IsAntiSnipingEnabled { get; set; } = true;

    public Guid? WinnerId { get; set; }
    public User? Winner { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public Order? Order { get; set; }

    public bool IsOwnedBy(Guid sellerId) => Product.SellerId == sellerId;

    /// <summary>Scheduled → Active guard.</summary>
    public bool Activate(DateTime now)
    {
        if (!AuctionStateMachine.CanActivate(Status) || now < StartTime)
        {
            return false;
        }

        Status = AuctionSessionStatus.Active;
        return true;
    }

    /// <summary>Only a Scheduled auction owned by the caller can be cancelled.</summary>
    public bool CancelBy(Guid sellerId)
    {
        if (!AuctionStateMachine.CanCancel(Status) || !IsOwnedBy(sellerId))
        {
            return false;
        }

        Status = AuctionSessionStatus.Cancelled;
        return true;
    }

    /// <summary>
    /// Validates and applies a bid following the exact rules used by the Redis
    /// Lua hot-path. Trusted only for SQL-side rules; the hot path decides in Lua.
    /// </summary>
    public BidAcceptance PlaceBid(Guid bidderId, decimal amount, DateTime now)
    {
        var decision = AuctionStateMachine.ValidateBid(
            Status, now, StartTime, EndTime, amount, StepPrice, CurrentPrice);

        if (decision != BidDecision.Accepted)
        {
            return new BidAcceptance(false, decision, false, null);
        }

        var isExtended = false;
        DateTime? newEndTime = null;

        var extension = AuctionStateMachine.ApplyAntiSniping(
            EndTime, now, IsAntiSnipingEnabled,
            AntiSnipingPolicy.SnipeWindowSeconds, AntiSnipingPolicy.ExtensionSeconds);

        if (extension.HasValue)
        {
            EndTime = extension.Value;
            isExtended = true;
            newEndTime = extension;
        }

        CurrentPrice = amount;
        WinnerId = bidderId;

        return new BidAcceptance(true, BidDecision.Accepted, isExtended, newEndTime);
    }

    /// <summary>Active → Ended. Idempotent on subsequent calls.</summary>
    public bool Finalize(Guid? winnerId, decimal finalAmount)
    {
        if (!AuctionStateMachine.CanFinalize(Status))
        {
            return false;
        }

        Status = AuctionSessionStatus.Ended;
        WinnerId = winnerId;
        CurrentPrice = finalAmount;
        return true;
    }
}
