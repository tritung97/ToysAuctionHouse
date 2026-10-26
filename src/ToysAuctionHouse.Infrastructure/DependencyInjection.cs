using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Infrastructure.Caching;
using ToysAuctionHouse.Infrastructure.Persistence;
using ToysAuctionHouse.Infrastructure.Persistence.Repositories;
using ToysAuctionHouse.Infrastructure.Redis;

namespace ToysAuctionHouse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer");

        services.AddDbContext<AuctionDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<AuctionDbContext>();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetSection("Redis")["Configuration"] ?? "localhost:6379";
            options.InstanceName = configuration.GetSection("Redis")["InstanceName"] ?? "ToysAuction:";
        });

        services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
        services.AddSingleton<IRedisBidRepository, RedisBidRepository>();
        services.AddSingleton<IRedisAuctionRepository, RedisAuctionRepository>();

        services.AddScoped<CacheAsideService>();
        services.AddScoped<ISqlUserRepository, SqlUserRepository>();
        services.AddScoped<ISqlProductRepository, SqlProductRepository>();
        services.AddScoped<ISqlAuctionRepository, SqlAuctionRepository>();
        services.AddScoped<ISqlOrderRepository, SqlOrderRepository>();

        services.AddHostedService<Background.AuctionActivatorService>();
        services.AddHostedService<Background.AuctionFinalizerService>();
        services.AddHostedService<Background.BidPersistorService>();

        return services;
    }
}
