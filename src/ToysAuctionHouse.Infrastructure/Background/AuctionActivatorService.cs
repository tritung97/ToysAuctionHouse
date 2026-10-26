using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Infrastructure.Background;

/// <summary>Promotes Scheduled → Active auctions and initializes the Redis hot store.</summary>
public sealed class AuctionActivatorService : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuctionActivatorService(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
        : base(TimeSpan.FromSeconds(5), loggerFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteTickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var auctions = scope.ServiceProvider.GetRequiredService<ISqlAuctionRepository>();
        var redis = scope.ServiceProvider.GetRequiredService<IRedisAuctionRepository>();
        var notifier = scope.ServiceProvider.GetRequiredService<IAuctionNotifier>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AuctionActivatorService>>();

        var due = await auctions.GetScheduledDueAsync(DateTime.UtcNow, ct).ConfigureAwait(false);
        foreach (var session in due)
        {
            if (!session.Activate(DateTime.UtcNow))
            {
                continue;
            }

            await auctions.UpdateStatusAsync(session.Id, AuctionSessionStatus.Active, ct).ConfigureAwait(false);
            await redis.InitSessionAsync(session, ct).ConfigureAwait(false);
            await notifier.AuctionStartedAsync(session.Id, session.StartTime, ct).ConfigureAwait(false);

            logger.LogInformation("Activated auction {AuctionId}", session.Id);
        }
    }
}
