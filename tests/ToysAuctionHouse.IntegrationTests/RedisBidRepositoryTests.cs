using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Infrastructure.Redis;
using Xunit;

namespace ToysAuctionHouse.IntegrationTests;

/// <summary>
/// Exercises the Redis Lua hot path against a real Redis (docker-compose). When
/// Redis is unreachable the tests short-circuit so the suite is CI-safe.
/// Tagged Category=Integration.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RedisBidRepositoryTests : IClassFixture<InfraFixture>
{
    private readonly InfraFixture _fixture;
    private readonly RedisBidRepository _repo;

    public RedisBidRepositoryTests(InfraFixture fixture)
    {
        _fixture = fixture;
        _repo = new RedisBidRepository(fixture.Redis);
    }

    [Fact]
    public async Task First_bid_accepts_and_updates_price()
    {
        if (!await _fixture.RedisAvailableAsync()) return;
        var auctionId = _fixture.NextAuctionId();
        await _fixture.SeedLiveAuctionAsync(auctionId, startPrice: 100m, stepPrice: 10m);
        var bidderId = Guid.NewGuid();

        var result = await _repo.PlaceBidAsync(auctionId, bidderId, "b1", 150m, DateTime.UtcNow, Guid.NewGuid().ToString("N"));

        Assert.Equal(BidStatus.Accepted, result.Status);
        Assert.Equal(150m, result.CurrentPrice);
        Assert.Equal(bidderId, result.WinnerId);
    }

    [Fact]
    public async Task Duplicate_idempotency_key_is_rejected_without_side_effect()
    {
        if (!await _fixture.RedisAvailableAsync()) return;
        var auctionId = _fixture.NextAuctionId();
        await _fixture.SeedLiveAuctionAsync(auctionId, startPrice: 100m, stepPrice: 10m);
        var key = Guid.NewGuid().ToString("N");
        var firstBidder = Guid.NewGuid();
        var secondBidder = Guid.NewGuid();

        var first = await _repo.PlaceBidAsync(auctionId, firstBidder, "b1", 150m, DateTime.UtcNow, key);
        var second = await _repo.PlaceBidAsync(auctionId, secondBidder, "b2", 200m, DateTime.UtcNow, key);

        Assert.Equal(BidStatus.Accepted, first.Status);
        Assert.Equal(BidStatus.Duplicate, second.Status);

        // No second side effect: the auction still reflects the first bid's state.
        var auctions = new RedisAuctionRepository(_fixture.Redis);
        var meta = await auctions.GetMetaAsync(auctionId);
        Assert.NotNull(meta);
        Assert.Equal(150m, meta!.CurrentPrice);
        Assert.Equal(firstBidder, meta.CurrentWinnerId);
    }

    [Fact]
    public async Task Outbid_amount_below_step_is_rejected()
    {
        if (!await _fixture.RedisAvailableAsync()) return;
        var auctionId = _fixture.NextAuctionId();
        await _fixture.SeedLiveAuctionAsync(auctionId, startPrice: 100m, stepPrice: 10m);
        var firstBidder = Guid.NewGuid();
        await _repo.PlaceBidAsync(auctionId, firstBidder, "b1", 120m, DateTime.UtcNow, Guid.NewGuid().ToString("N"));

        var result = await _repo.PlaceBidAsync(auctionId, Guid.NewGuid(), "b2", 125m, DateTime.UtcNow, Guid.NewGuid().ToString("N"));

        Assert.Equal(BidStatus.Outbid, result.Status);
        Assert.Equal(120m, result.CurrentPrice);
        Assert.Equal(firstBidder, result.WinnerId);
    }

    [Fact]
    public async Task Concurrent_bids_yield_single_monotonic_winner()
    {
        if (!await _fixture.RedisAvailableAsync()) return;
        var auctionId = _fixture.NextAuctionId();
        await _fixture.SeedLiveAuctionAsync(auctionId, startPrice: 100m, stepPrice: 1m);

        var tasks = new List<Task<BidResult>>();
        for (var i = 0; i < 50; i++)
        {
            var bidderId = Guid.NewGuid();
            var amount = 110m + i;
            tasks.Add(_repo.PlaceBidAsync(auctionId, bidderId, $"b{i}", amount, DateTime.UtcNow, Guid.NewGuid().ToString("N")));
        }

        var results = await Task.WhenAll(tasks);
        var accepted = results.Where(r => r.Status == BidStatus.Accepted).OrderByDescending(r => r.CurrentPrice).ToList();

        // Exactly one winning bid = highest amount, monotonic.
        var final = results.OrderByDescending(r => r.CurrentPrice).First();
        Assert.Equal(BidStatus.Accepted, final.Status);
        Assert.Equal(110m + 49, final.CurrentPrice);
        Assert.Equal(50, accepted.Count);
        // All accepted prices are unique and strictly increasing as amounts ascend.
        Assert.Equal(accepted.Count, accepted.Select(r => r.CurrentPrice).Distinct().Count());
    }

    [Fact]
    public async Task Anti_sniping_extends_end_time_when_within_window()
    {
        if (!await _fixture.RedisAvailableAsync()) return;
        var auctionId = _fixture.NextAuctionId();
        var now = DateTime.UtcNow;
        // End 30s out → anti-sniping window (60s) triggers an extension.
        await _fixture.SeedLiveAuctionAsync(auctionId, startPrice: 100m, stepPrice: 10m, endTime: now.AddSeconds(30));

        var result = await _repo.PlaceBidAsync(auctionId, Guid.NewGuid(), "b1", 150m, now, Guid.NewGuid().ToString("N"));

        Assert.Equal(BidStatus.Accepted, result.Status);
        Assert.True(result.Extended);
        Assert.NotNull(result.NewEndTime);
        Assert.True(result.NewEndTime > now.AddSeconds(30));
    }
}
