using StackExchange.Redis;

namespace ToysAuctionHouse.Infrastructure.Redis;

/// <summary>
/// The single Redis Lua script that runs the entire bid hot-path atomically.
///
/// KEYS[1] = meta hash        (auction:{id}:meta)
/// KEYS[2] = bids zset        (auction:{id}:bids)
/// KEYS[3] = idempotency key  (idempotency:{key})
///
/// ARGV[1] = amount (string, 2dp)
/// ARGV[2] = bidderId (int)
/// ARGV[3] = bidId (string)
/// ARGV[4] = now (unix milliseconds)
/// ARGV[5] = snipeWindowSeconds
/// ARGV[6] = extensionSeconds
///
/// Returns a flat array:
///   [1] status   ACCEPTED | DUPLICATE | OUTBID | ENDED | NOT_STARTED | NOT_FOUND
///   [2] currentPrice (string 2dp)
///   [3] winnerId (string) or ''
///   [4] extended ('1'/'0')
///   [5] newEndTime (unix ms) or ''
/// </summary>
public static class AuctionBidLua
{
    public const string Script = """
        local amount = tonumber(ARGV[1])
        local now = tonumber(ARGV[4])

        local ok = redis.call('SET', KEYS[3], 'PROCESSING', 'EX', 30, 'NX')
        if not ok then
            return {'DUPLICATE', '0', '', '0', ''}
        end

        local meta = redis.call('HGETALL', KEYS[1])
        if #meta == 0 then
            return {'NOT_FOUND', '0', '', '0', ''}
        end

        local m = {}
        for i = 1, #meta, 2 do m[meta[i]] = meta[i + 1] end

        local status = m['Status']
        local startTime = tonumber(m['StartTime'])
        local endTime = tonumber(m['EndTime'])
        local currentPrice = tonumber(m['CurrentPrice'])
        local stepPrice = tonumber(m['StepPrice'])
        local isAntiSniping = m['IsAntiSnipingEnabled']

        if status == '4' then
            return {'ENDED', m['CurrentPrice'] or '0', m['CurrentWinnerId'] or '', '0', ''}
        end
        if status ~= '2' then
            return {'NOT_STARTED', m['CurrentPrice'] or '0', m['CurrentWinnerId'] or '', '0', ''}
        end

        if now < startTime then
            return {'NOT_STARTED', m['CurrentPrice'] or '0', m['CurrentWinnerId'] or '', '0', ''}
        end
        if now >= endTime then
            return {'ENDED', m['CurrentPrice'] or '0', m['CurrentWinnerId'] or '', '0', ''}
        end

        if amount < currentPrice + stepPrice then
            return {'OUTBID', string.format('%.2f', currentPrice), m['CurrentWinnerId'] or '', '0', ''}
        end

        local member = ARGV[2] .. ':' .. ARGV[3] .. ':' .. tostring(now)
        redis.call('HSET', KEYS[1], 'CurrentPrice', string.format('%.2f', amount))
        redis.call('HSET', KEYS[1], 'CurrentWinnerId', ARGV[2])
        redis.call('ZADD', KEYS[2], string.format('%.2f', amount), member)

        local extended = '0'
        local newEndTime = m['EndTime']
        if isAntiSniping == '1' then
            local remainingMs = endTime - now
            local snipeMs = tonumber(ARGV[5]) * 1000
            local extMs = tonumber(ARGV[6]) * 1000
            if remainingMs > 0 and remainingMs <= snipeMs then
                newEndTime = tostring(now + extMs)
                redis.call('HSET', KEYS[1], 'EndTime', newEndTime)
                extended = '1'
            end
        end

        return {'ACCEPTED', string.format('%.2f', amount), ARGV[2], extended, newEndTime}
        """;

    public static RedisValue[] Evaluate(IDatabase db, RedisKey[] keys, RedisValue[] values)
    {
        var result = db.ScriptEvaluate(Script, keys, values);
        if (result.IsNull)
        {
            return Array.Empty<RedisValue>();
        }

        RedisValue[]? array = (RedisValue[]?)result;
        return array ?? Array.Empty<RedisValue>();
    }
}
