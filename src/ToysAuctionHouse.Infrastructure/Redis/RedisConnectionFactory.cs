using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace ToysAuctionHouse.Infrastructure.Redis;

public interface IRedisConnectionFactory
{
    IDatabase GetDatabase();
    IConnectionMultiplexer GetMultiplexer();
}

public sealed class RedisConnectionFactory : IRedisConnectionFactory
{
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisConnectionFactory(IConfiguration configuration)
    {
        var config = configuration.GetSection("Redis")["Configuration"] ?? "localhost:6379";
        _connection = new Lazy<ConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(config),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IDatabase GetDatabase() => _connection.Value.GetDatabase();

    public IConnectionMultiplexer GetMultiplexer() => _connection.Value;
}
