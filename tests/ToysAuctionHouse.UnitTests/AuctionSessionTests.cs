using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Domain.StateMachine;
using Xunit;

namespace ToysAuctionHouse.UnitTests;

public class AuctionSessionTests
{
    private static AuctionSession ActiveSession()
    {
        var now = DateTime.UtcNow;
        return new AuctionSession
        {
            ProductId = Guid.NewGuid(),
            StartPrice = 100,
            StepPrice = 10,
            CurrentPrice = 100,
            StartTime = now.AddMinutes(-5),
            EndTime = now.AddHours(1),
            Status = AuctionSessionStatus.Active,
        };
    }

    [Fact]
    public void Activate_transitions_scheduled_to_active()
    {
        var now = DateTime.UtcNow;
        var session = new AuctionSession
        {
            StartTime = now.AddSeconds(-1),
            Status = AuctionSessionStatus.Scheduled,
        };

        Assert.True(session.Activate(now));
        Assert.Equal(AuctionSessionStatus.Active, session.Status);
    }

    [Fact]
    public void Activate_fails_before_start_time()
    {
        var now = DateTime.UtcNow;
        var session = new AuctionSession
        {
            StartTime = now.AddMinutes(5),
            Status = AuctionSessionStatus.Scheduled,
        };

        Assert.False(session.Activate(now));
    }

    [Fact]
    public void PlaceBid_updates_price_winner_and_sets_winner()
    {
        var session = ActiveSession();
        var bidderId = Guid.NewGuid();

        var result = session.PlaceBid(bidderId, 150m, DateTime.UtcNow);

        Assert.True(result.Accepted);
        Assert.Equal(150m, session.CurrentPrice);
        Assert.Equal(bidderId, session.WinnerId);
    }

    [Fact]
    public void PlaceBid_rejects_outbid_amount()
    {
        var session = ActiveSession();

        var result = session.PlaceBid(Guid.NewGuid(), 105m, DateTime.UtcNow);

        Assert.False(result.Accepted);
        Assert.Equal(BidDecision.InvalidAmount, result.Decision);
        Assert.Equal(100m, session.CurrentPrice);
    }

    [Fact]
    public void Finalize_sets_ended_and_winner()
    {
        var session = ActiveSession();
        var winnerId = Guid.NewGuid();

        Assert.True(session.Finalize(winnerId, 150m));
        Assert.Equal(AuctionSessionStatus.Ended, session.Status);
        Assert.Equal(winnerId, session.WinnerId);
        Assert.Equal(150m, session.CurrentPrice);
    }
}
