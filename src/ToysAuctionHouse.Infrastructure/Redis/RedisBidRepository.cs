using StackExchange.Redis;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Domain.Constants;

namespace ToysAuctionHouse.Infrastructure.Redis;

public sealed class RedisBidRepository : IRedisBidRepository
{
    private readonly IDatabase _db;

    public RedisBidRepository(IRedisConnectionFactory factory)
    {
        _db = factory.GetDatabase();
    }

    public async Task<BidResult> PlaceBidAsync(
        Guid auctionId,
        Guid bidderId,
        string bidId,
        decimal amount,
        DateTime now,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var keys = new RedisKey[]
        {
            AuctionKeyBuilder.Meta(auctionId),
            AuctionKeyBuilder.Bids(auctionId),
            AuctionKeyBuilder.Idempotency(idempotencyKey),
        };

        var args = new RedisValue[]
        {
            FormatAmount(amount),
            bidderId.ToString(),
            bidId,
            ToUnixMilliseconds(now).ToString(),
            AntiSnipingPolicy.SnipeWindowSeconds.ToString(),
            AntiSnipingPolicy.ExtensionSeconds.ToString(),
        };

        var result = AuctionBidLua.Evaluate(_db, keys, args);

        var values = result;
        var status = ParseStatus(values[0]);
        var currentPrice = ParseDecimal(values[1]);
        Guid? winnerId = string.IsNullOrEmpty(values[2]) ? null : Guid.Parse(values[2]!);
        var extended = values[3] == "1";
        DateTime? newEndTime = string.IsNullOrEmpty(values[4])
            ? null
            : FromUnixMilliseconds(long.Parse(values[4]!));

        var bidResult = new BidResult(status, currentPrice, winnerId, extended, newEndTime);

        if (status == BidStatus.Accepted)
        {
            await AddBidToStreamAsync(auctionId, bidderId, bidId, amount, now, idempotencyKey, ct).ConfigureAwait(false);
        }

        return bidResult;
    }

    public async Task AddBidToStreamAsync(
        Guid auctionId,
        Guid bidderId,
        string bidId,
        decimal amount,
        DateTime timestamp,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var values = new NameValueEntry[]
        {
            new("auctionId", auctionId.ToString()),
            new("bidderId", bidderId.ToString()),
            new("bidId", bidId),
            new("amount", FormatAmount(amount)),
            new("idempotencyKey", idempotencyKey),
            new("timestamp", ToUnixMilliseconds(timestamp).ToString()),
        };

        await _db.StreamAddAsync(AuctionKeyBuilder.BidsStreamKey, values, maxLength: 100_000, useApproximateMaxLength: true).ConfigureAwait(false);
    }

    public async Task CreateBidsConsumerGroupAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(
                AuctionKeyBuilder.BidsStreamKey,
                AuctionKeyBuilder.BidsConsumerGroup,
                position: null,
                createStream: true).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists.
        }
    }

    public async Task<IReadOnlyList<BidStreamMessage>> ReadBidsAsync(int maxCount, CancellationToken ct = default)
    {
        var entries = await _db.StreamReadGroupAsync(
            AuctionKeyBuilder.BidsStreamKey,
            AuctionKeyBuilder.BidsConsumerGroup,
            AuctionKeyBuilder.ConsumerName,
            maxCount).ConfigureAwait(false);

        var messages = new List<BidStreamMessage>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Values;
            string Get(string name)
            {
                var field = fields.FirstOrDefault(f => f.Name == name);
                return field.Value.IsNull ? string.Empty : field.Value.ToString();
            }

            if (!Guid.TryParse(Get("auctionId"), out var auctionId) ||
                !Guid.TryParse(Get("bidderId"), out var bidderId) ||
                !decimal.TryParse(Get("amount"), out var amount))
            {
                continue;
            }

            var bidId = Get("bidId");
            var idempotencyKey = Get("idempotencyKey");
            long ts = long.TryParse(Get("timestamp"), out var t) ? t : ToUnixMilliseconds(DateTime.UtcNow);

            messages.Add(new BidStreamMessage(entry.Id.ToString(), auctionId, bidderId, bidId, amount, idempotencyKey, FromUnixMilliseconds(ts)));
        }

        return messages;
    }

    public async Task AckBidAsync(string messageId, CancellationToken ct = default)
    {
        await _db.StreamAcknowledgeAsync(AuctionKeyBuilder.BidsStreamKey, AuctionKeyBuilder.BidsConsumerGroup, messageId).ConfigureAwait(false);
    }

    private static BidStatus ParseStatus(RedisValue value)
    {
        return value.ToString() switch
        {
            "ACCEPTED" => BidStatus.Accepted,
            "DUPLICATE" => BidStatus.Duplicate,
            "OUTBID" => BidStatus.Outbid,
            "ENDED" => BidStatus.Ended,
            "NOT_STARTED" => BidStatus.NotStarted,
            _ => BidStatus.NotFound,
        };
    }

    private static RedisValue[] ToArray(RedisValue[] result) => result;

    private static string FormatAmount(decimal amount) => amount.ToString("0.00");

    private static decimal ParseDecimal(RedisValue value)
        => decimal.TryParse(value, out var d) ? d : 0m;

    public static long ToUnixMilliseconds(DateTime dt) => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    public static DateTime FromUnixMilliseconds(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
}
