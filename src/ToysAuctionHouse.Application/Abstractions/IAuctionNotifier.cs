namespace ToysAuctionHouse.Application.Abstractions;

/// <summary>
/// Abstraction that pushes real-time events to the SignalR group for an auction.
/// Implemented in the API layer via <c>IHubContext&lt;AuctionHub&gt;</c> so that
/// Infrastructure background jobs never reference the Hub directly.
/// </summary>
public interface IAuctionNotifier
{
    Task NewBidAsync(Guid auctionId, string bidderName, decimal amount, DateTime timestamp, CancellationToken ct = default);

    Task AuctionExtendedAsync(Guid auctionId, DateTime newEndTime, CancellationToken ct = default);

    Task AuctionStartedAsync(Guid auctionId, DateTime startTime, CancellationToken ct = default);

    Task AuctionEndedAsync(Guid auctionId, string? winnerName, decimal finalAmount, CancellationToken ct = default);
}
