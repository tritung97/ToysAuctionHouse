using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Application.Abstractions.Repositories;

namespace ToysAuctionHouse.Infrastructure.Background;

/// <summary>
/// Continuously drains the bid stream from a Redis consumer group and persists
/// accepted bids into SQL. ACKs are issued only after a durable insert, so a
/// crash redelivers un-ACKed messages (idempotent via the unique IdempotencyKey).
/// </summary>
public sealed class BidPersistorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BidPersistorService> _logger;

    public BidPersistorService(IServiceScopeFactory scopeFactory, ILogger<BidPersistorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var redis = scope.ServiceProvider.GetRequiredService<IRedisBidRepository>();
            await redis.CreateBidsConsumerGroupAsync(stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IRedisBidRepository>();
                var auctions = scope.ServiceProvider.GetRequiredService<ISqlAuctionRepository>();

                var messages = await redis.ReadBidsAsync(200, stoppingToken).ConfigureAwait(false);
                if (messages.Count == 0)
                {
                    await Task.Delay(500, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var items = messages
                    .Select(m => new BidPersistItem(m.AuctionId, m.BidderId, m.Amount, m.IdempotencyKey, m.Timestamp))
                    .ToList();

                await auctions.PersistBidsAsync(items, stoppingToken).ConfigureAwait(false);

                foreach (var message in messages)
                {
                    await redis.AckBidAsync(message.MessageId, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BidPersistor tick failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
