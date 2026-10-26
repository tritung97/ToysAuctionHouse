using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Infrastructure.Persistence;
using ToysAuctionHouse.Infrastructure.Redis;

namespace ToysAuctionHouse.IntegrationTests;

/// <summary>
/// Shared infra for integration tests. Requires a reachable Redis (and SQL for the
/// repo/finalizer tests). See docker-compose.yml. Tests are tagged
/// [Trait("Category","Integration")] and silently pass-with-grace when the
/// dependency is missing so CI without infra doesn't fail on connection errors.
/// </summary>
public sealed class InfraFixture : IAsyncLifetime
{
    public IConfiguration Configuration { get; }
    public IRedisConnectionFactory Redis { get; }
    public string? SqlConnection { get; }
    public DbContextOptions<AuctionDbContext>? SqlOptions { get; }

    public InfraFixture()
    {
        var redisConn = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = redisConn,
            })
            .Build();

        Redis = new RedisConnectionFactory(Configuration);

        SqlConnection = Environment.GetEnvironmentVariable("SQL_CONNECTION")
            ?? "Server=localhost,1433;Database=ToysAuction_Test;User Id=sa;Password=LocalDev!Passw0rd;Encrypt=False;TrustServerCertificate=True";

        SqlOptions = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseSqlServer(SqlConnection)
            .Options;
    }

    public async Task InitializeAsync()
    {
        await RedisAvailableAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task<bool> RedisAvailableAsync()
    {
        try
        {
            var db = Redis.GetDatabase();
            await db.ExecuteAsync("PING");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SqlAvailable()
    {
        if (SqlOptions is null) return false;
        try
        {
            using var db = new AuctionDbContext(SqlOptions);
            return db.Database.CanConnect();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Generates a unique auction id without clashing with previously stored test data.</summary>
    public Guid NextAuctionId() => Guid.NewGuid();

    public async Task SeedLiveAuctionAsync(
        Guid auctionId,
        decimal startPrice = 100m,
        decimal stepPrice = 10m,
        Guid sellerId = default,
        Guid productId = default,
        DateTime? endTime = null,
        bool antiSniping = true)
    {
        var now = DateTime.UtcNow;
        var session = new AuctionSession
        {
            Id = auctionId,
            ProductId = productId,
            Product = new ToyProduct { Id = productId, SellerId = sellerId },
            StartPrice = startPrice,
            StepPrice = stepPrice,
            CurrentPrice = startPrice,
            StartTime = now.AddMinutes(-5),
            EndTime = endTime ?? now.AddHours(1),
            Status = AuctionSessionStatus.Active,
            IsAntiSnipingEnabled = antiSniping,
        };

        var repo = new RedisAuctionRepository(Redis);
        await repo.CleanupAsync(auctionId);
        await repo.InitSessionAsync(session);
    }
}
