using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;

namespace ToysAuctionHouse.Infrastructure.Background;

/// <summary>
/// Detects expired live auctions in Redis, atomically marks them ended, and
/// persists the final ledger state (session + order) via a single SQL transaction.
/// Bids themselves are persisted continuously by <see cref="BidPersistorService"/>.
/// </summary>
public sealed class AuctionFinalizerService : PeriodicBackgroundService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;

    public AuctionFinalizerService(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
        : base(TimeSpan.FromSeconds(1), loggerFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteTickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<IRedisAuctionRepository>();
        var auctions = scope.ServiceProvider.GetRequiredService<ISqlAuctionRepository>();
        var users = scope.ServiceProvider.GetRequiredService<ISqlUserRepository>();
        var notifier = scope.ServiceProvider.GetRequiredService<IAuctionNotifier>();

        var expired = await redis.GetExpiredActiveAsync(DateTime.UtcNow, ct).ConfigureAwait(false);
        foreach (var auctionId in expired)
        {
            await redis.MarkEndedAsync(auctionId, ct).ConfigureAwait(false);

            var winning = await redis.GetWinningBidAsync(auctionId, ct).ConfigureAwait(false);
            var winnerId = winning?.BidderId;
            var finalAmount = winning?.Amount ?? 0m;

            var orderId = await auctions
                .FinalizeAndPersistAsync(new FinalizeCommand(auctionId, winnerId, finalAmount, Array.Empty<BidPersistItem>()), ct)
                .ConfigureAwait(false);

            await redis.RemoveFromActiveListAsync(auctionId, ct).ConfigureAwait(false);

            string? winnerName = null;
            if (winnerId.HasValue)
            {
                var winner = await users.GetByIdAsync(winnerId.Value, ct).ConfigureAwait(false);
                winnerName = winner?.FullName;
            }

            await notifier.AuctionEndedAsync(auctionId, winnerName, finalAmount, ct).ConfigureAwait(false);

            _ = CleanupAfterGraceAsync(redis, auctionId, ct);
        }
    }

    private static async Task CleanupAfterGraceAsync(IRedisAuctionRepository redis, Guid auctionId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(GracePeriod, ct).ConfigureAwait(false);
            await redis.CleanupAsync(auctionId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Service shutting down — skip cleanup.
        }
    }
}
