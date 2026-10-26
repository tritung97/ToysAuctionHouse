using Microsoft.AspNetCore.SignalR;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Api.Hubs;
using ToysAuctionHouse.Api.Hubs.Contracts;
using ToysAuctionHouse.Infrastructure.Redis;

namespace ToysAuctionHouse.Api;

public sealed class SignalRAuctionNotifier : IAuctionNotifier
{
    private readonly IHubContext<AuctionHub> _hub;

    public SignalRAuctionNotifier(IHubContext<AuctionHub> hub)
    {
        _hub = hub;
    }

    public Task NewBidAsync(Guid auctionId, string bidderName, decimal amount, DateTime timestamp, CancellationToken ct = default)
        => _hub.Clients.Group(AuctionKeyBuilder.RoomGroup(auctionId))
            .SendAsync(HubContract.ReceiveNewBid, new { bidderName, amount, timestamp }, ct);

    public Task AuctionExtendedAsync(Guid auctionId, DateTime newEndTime, CancellationToken ct = default)
        => _hub.Clients.Group(AuctionKeyBuilder.RoomGroup(auctionId))
            .SendAsync(HubContract.AuctionExtended, newEndTime, ct);

    public Task AuctionStartedAsync(Guid auctionId, DateTime startTime, CancellationToken ct = default)
        => _hub.Clients.All.SendAsync(HubContract.AuctionStarted, new { auctionId, startTime }, ct);

    public Task AuctionEndedAsync(Guid auctionId, string? winnerName, decimal finalAmount, CancellationToken ct = default)
        => _hub.Clients.Group(AuctionKeyBuilder.RoomGroup(auctionId))
            .SendAsync(HubContract.AuctionEnded, new { auctionId, winnerName, finalAmount }, ct);
}
