using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Domain.StateMachine;
using Xunit;

namespace ToysAuctionHouse.UnitTests;

public class AuctionStateMachineTests
{
    private const int Snipe = 60;
    private const int Extension = 120;

    [Theory]
    [InlineData(AuctionSessionStatus.Scheduled, false)]
    [InlineData(AuctionSessionStatus.Active, true)]
    [InlineData(AuctionSessionStatus.Ended, false)]
    [InlineData(AuctionSessionStatus.Cancelled, false)]
    public void CanBid_only_active(AuctionSessionStatus status, bool expected)
        => Assert.Equal(expected, AuctionStateMachine.CanBid(status));

    [Fact]
    public void ValidateBid_accepts_when_active_and_amount_valid()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var decision = AuctionStateMachine.ValidateBid(
            AuctionSessionStatus.Active, now, now.AddHours(-1), now.AddHours(1), 110m, 10m, 100m);

        Assert.Equal(BidDecision.Accepted, decision);
    }

    [Fact]
    public void ValidateBid_rejects_when_amount_below_step()
        => Assert.Equal(
            BidDecision.InvalidAmount,
            AuctionStateMachine.ValidateBid(AuctionSessionStatus.Active, DateTime.UtcNow, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), 105m, 10m, 100m));

    [Fact]
    public void ValidateBid_rejects_after_end()
        => Assert.Equal(
            BidDecision.AuctionEnded,
            AuctionStateMachine.ValidateBid(AuctionSessionStatus.Active, DateTime.UtcNow, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), 200m, 10m, 100m));

    [Fact]
    public void ValidateBid_rejects_when_not_started_yet()
        => Assert.Equal(
            BidDecision.AuctionNotStarted,
            AuctionStateMachine.ValidateBid(AuctionSessionStatus.Active, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 200m, 10m, 100m));

    [Fact]
    public void AntiSniping_extends_when_within_window()
    {
        var now = new DateTime(2026, 1, 1, 12, 59, 0, DateTimeKind.Utc); // 60s before end
        var end = new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc);

        var result = AuctionStateMachine.ApplyAntiSniping(end, now, true, Snipe, Extension);

        Assert.NotNull(result);
        Assert.Equal(now.AddSeconds(Extension), result);
    }

    [Fact]
    public void AntiSniping_no_extension_outside_window()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc);

        Assert.Null(AuctionStateMachine.ApplyAntiSniping(end, now, true, Snipe, Extension));
    }

    [Fact]
    public void AntiSniping_disabled_never_extends()
    {
        var now = new DateTime(2026, 1, 1, 12, 59, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc);

        Assert.Null(AuctionStateMachine.ApplyAntiSniping(end, now, false, Snipe, Extension));
    }
}
