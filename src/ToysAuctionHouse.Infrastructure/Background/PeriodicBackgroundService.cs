using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ToysAuctionHouse.Infrastructure.Background;

public abstract class PeriodicBackgroundService : BackgroundService
{
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;

    protected PeriodicBackgroundService(TimeSpan interval, ILoggerFactory loggerFactory)
    {
        _interval = interval;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job {Type} tick failed", GetType().Name);
            }

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }

    protected abstract Task ExecuteTickAsync(CancellationToken ct);
}
