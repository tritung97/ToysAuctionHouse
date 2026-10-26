using ToysAuctionHouse.Infrastructure.Redis;
using Xunit;

namespace ToysAuctionHouse.UnitTests;

/// <summary>
/// A static fidelity check that the Redis Lua hot path and the domain state
/// machine agree on the bid/anti-sniping contract. Full runtime agreement is
/// exercised by integration tests (Phase 10) against a real Redis.
/// </summary>
public class BidLuaFidelityTests
{
    [Fact]
    public void Lua_script_performs_step_price_guard()
    {
        // Mirrors the domain: a bid is rejected when amount < currentPrice + stepPrice.
        Assert.Contains("currentPrice + stepPrice", AuctionBidLua.Script);
    }

    [Fact]
    public void Lua_script_blocks_bids_after_end_time()
    {
        Assert.Contains("now >= endTime", AuctionBidLua.Script);
    }

    [Fact]
    public void Lua_script_uses_idempotency_setnx()
    {
        Assert.Contains("NX", AuctionBidLua.Script);
        Assert.Contains("'PROCESSING'", AuctionBidLua.Script);
        Assert.Contains("'EX'", AuctionBidLua.Script);
    }

    [Fact]
    public void Lua_script_applies_anti_sniping_with_extension_arithmetic()
    {
        Assert.Contains("remainingMs", AuctionBidLua.Script);
        Assert.Contains("snipeMs", AuctionBidLua.Script);
        Assert.Contains("extMs", AuctionBidLua.Script);
        Assert.Contains("* 1000", AuctionBidLua.Script);
    }

    [Fact]
    public void Lua_script_returns_typed_outcomes()
    {
        Assert.Contains("'ACCEPTED'", AuctionBidLua.Script);
        Assert.Contains("'DUPLICATE'", AuctionBidLua.Script);
        Assert.Contains("'OUTBID'", AuctionBidLua.Script);
        Assert.Contains("'ENDED'", AuctionBidLua.Script);
    }
}
