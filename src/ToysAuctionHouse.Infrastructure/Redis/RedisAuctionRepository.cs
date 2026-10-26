using StackExchange.Redis;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Infrastructure.Redis;

public sealed class RedisAuctionRepository : IRedisAuctionRepository
{
    private readonly IDatabase _db;

    public RedisAuctionRepository(IRedisConnectionFactory factory)
    {
        _db = factory.GetDatabase();
    }

    public async Task InitSessionAsync(AuctionSession session, CancellationToken ct = default)
    {
        var metaKey = AuctionKeyBuilder.Meta(session.Id);

        var entries = new HashEntry[]
        {
            new("Status", ((byte)session.Status).ToString()),
            new("StartPrice", FormatAmount(session.StartPrice)),
            new("StepPrice", FormatAmount(session.StepPrice)),
            new("CurrentPrice", FormatAmount(session.CurrentPrice)),
            new("CurrentWinnerId", string.Empty),
            new("StartTime", RedisBidRepository.ToUnixMilliseconds(session.StartTime).ToString()),
            new("EndTime", RedisBidRepository.ToUnixMilliseconds(session.EndTime).ToString()),
            new("SellerId", session.Product.SellerId.ToString()),
            new("ProductId", session.ProductId.ToString()),
            new("IsAntiSnipingEnabled", session.IsAntiSnipingEnabled ? "1" : "0"),
            new("Version", "0"),
        };

        await _db.HashSetAsync(metaKey, entries).ConfigureAwait(false);
        await _db.SortedSetAddAsync(AuctionKeyBuilder.Bids(session.Id), Array.Empty<SortedSetEntry>()).ConfigureAwait(false);
        await _db.SetAddAsync(AuctionKeyBuilder.ActiveListKey, session.Id.ToString()).ConfigureAwait(false);
    }

    public async Task<AuctionSnapshot?> GetMetaAsync(Guid auctionId, CancellationToken ct = default)
    {
        var entries = await _db.HashGetAllAsync(AuctionKeyBuilder.Meta(auctionId)).ConfigureAwait(false);
        if (entries.Length == 0)
        {
            return null;
        }

        var m = entries.ToDictionary(x => (string)x.Name!, x => (string?)x.Value ?? string.Empty);

        return new AuctionSnapshot(
            auctionId,
            m.GetValueOrDefault("Status", "0"),
            ParseDecimal(m.GetValueOrDefault("StartPrice")),
            ParseDecimal(m.GetValueOrDefault("StepPrice")),
            ParseDecimal(m.GetValueOrDefault("CurrentPrice")),
            ParseNullableGuid(m.GetValueOrDefault("CurrentWinnerId")),
            RedisBidRepository.FromUnixMilliseconds(ParseLong(m.GetValueOrDefault("StartTime"))),
            RedisBidRepository.FromUnixMilliseconds(ParseLong(m.GetValueOrDefault("EndTime"))),
            ParseGuid(m.GetValueOrDefault("SellerId")),
            ParseGuid(m.GetValueOrDefault("ProductId")),
            m.GetValueOrDefault("IsAntiSnipingEnabled") == "1");
    }

    public async Task<IReadOnlyList<BidEntry>> GetTopBidsAsync(Guid auctionId, int count, CancellationToken ct = default)
    {
        var values = await _db.SortedSetRangeByRankWithScoresAsync(
            AuctionKeyBuilder.Bids(auctionId),
            start: -(count),
            stop: -1,
            order: StackExchange.Redis.Order.Descending).ConfigureAwait(false);

        return values.Select(ToBidEntry).ToList();
    }

    public async Task<BidEntry?> GetWinningBidAsync(Guid auctionId, CancellationToken ct = default)
    {
        var values = await _db.SortedSetRangeByRankWithScoresAsync(
            AuctionKeyBuilder.Bids(auctionId),
            start: 0,
            stop: 0,
            order: StackExchange.Redis.Order.Descending).ConfigureAwait(false);

        return values.Length == 0 ? null : ToBidEntry(values[0]);
    }

    public async Task<IReadOnlyList<Guid>> GetExpiredActiveAsync(DateTime now, CancellationToken ct = default)
    {
        var members = await _db.SetMembersAsync(AuctionKeyBuilder.ActiveListKey).ConfigureAwait(false);
        var nowMs = RedisBidRepository.ToUnixMilliseconds(now);
        var expired = new List<Guid>();

        foreach (var member in members)
        {
            if (!Guid.TryParse(member, out var auctionId))
            {
                continue;
            }

            var endValue = await _db.HashGetAsync(AuctionKeyBuilder.Meta(auctionId), "EndTime").ConfigureAwait(false);
            if (endValue.HasValue && long.TryParse(endValue, out var endMs) && endMs <= nowMs)
            {
                expired.Add(auctionId);
            }
        }

        return expired;
    }

    public async Task MarkEndedAsync(Guid auctionId, CancellationToken ct = default)
    {
        await _db.HashSetAsync(
            AuctionKeyBuilder.Meta(auctionId),
            new HashEntry[] { new("Status", ((byte)AuctionSessionStatus.Ended).ToString()) }).ConfigureAwait(false);
    }

    public async Task RemoveFromActiveListAsync(Guid auctionId, CancellationToken ct = default)
    {
        await _db.SetRemoveAsync(AuctionKeyBuilder.ActiveListKey, auctionId.ToString()).ConfigureAwait(false);
    }

    public async Task CleanupAsync(Guid auctionId, CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(new RedisKey[]
        {
            AuctionKeyBuilder.Meta(auctionId),
            AuctionKeyBuilder.Bids(auctionId),
        }).ConfigureAwait(false);
    }

    private static BidEntry ToBidEntry(SortedSetEntry entry)
    {
        var member = entry.Element.ToString();
        var parts = member.Split(':');
        var bidderId = parts.Length > 0 && Guid.TryParse(parts[0], out var b) ? b : Guid.Empty;
        long ts = parts.Length > 0 && long.TryParse(parts[^1], out var t) ? t : 0;
        var bidId = parts.Length >= 3 ? string.Join(":", parts[1..^1]) : member;

        return new BidEntry((decimal)entry.Score, bidderId, ts, bidId);
    }

    private static string FormatAmount(decimal amount) => amount.ToString("0.00");

    private static decimal ParseDecimal(string? s) => decimal.TryParse(s, out var d) ? d : 0m;

    private static Guid ParseGuid(string? s) => Guid.TryParse(s, out var g) ? g : Guid.Empty;

    private static Guid? ParseNullableGuid(string? s) => Guid.TryParse(s, out var g) ? g : null;

    private static long ParseLong(string? s) => long.TryParse(s, out var l) ? l : 0;
}
