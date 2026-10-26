using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ToysAuctionHouse.Api.Auth;
using ToysAuctionHouse.Api.Hubs.Contracts;
using ToysAuctionHouse.Application.Services;

namespace ToysAuctionHouse.Api.Hubs;

[Authorize]
public sealed class AuctionHub : Hub
{
    private readonly AuctionService _auctions;
    private readonly CatalogService _catalog;
    private readonly CurrentUserService _current;

    public AuctionHub(AuctionService auctions, CatalogService catalog, CurrentUserService current)
    {
        _auctions = auctions;
        _catalog = catalog;
        _current = current;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public async Task JoinAuctionRoom(Guid auctionId)
    {
        var group = AuctionKeyFor(auctionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);

        var room = await _catalog.GetRoomDetailAsync(auctionId, Context.ConnectionAborted).ConfigureAwait(false);
        if (room.Success)
        {
            await Clients.Caller.SendAsync(HubContract.RoomSnapshot, room.Value, Context.ConnectionAborted).ConfigureAwait(false);
        }
        else
        {
            await Clients.Caller.SendAsync(HubContract.BidRejected, new { status = "NOT_FOUND", message = room.Message }, Context.ConnectionAborted).ConfigureAwait(false);
        }
    }

    /// <summary>Leaves a room so a single connection can switch between auctions without stale events.</summary>
    public async Task LeaveAuctionRoom(Guid auctionId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, AuctionKeyFor(auctionId)).ConfigureAwait(false);

    public async Task SendBid(BidRequestDto request)
    {
        var group = AuctionKeyFor(request.AuctionId);

        var result = await _auctions
            .PlaceBidAsync(_current.UserId, request.AuctionId, request.Amount, request.IdempotencyKey, Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (!result.Success || result.Value is null)
        {
            await Clients.Caller.SendAsync(HubContract.BidRejected, new { status = "REJECTED", message = result.Message }, Context.ConnectionAborted).ConfigureAwait(false);
            return;
        }

        var bid = result.Value;
        if (bid.Status != "Accepted")
        {
            await Clients.Caller.SendAsync(HubContract.BidRejected, new { status = bid.Status, message = bid.Message }, Context.ConnectionAborted).ConfigureAwait(false);
            return;
        }

        var bidderName = _current.Username ?? $"bidder_{_current.UserId}";
        var payload = new { bidderId = _current.UserId, bidderName, amount = bid.CurrentPrice, timestamp = DateTime.UtcNow };

        await Clients.Group(group).SendAsync(HubContract.ReceiveNewBid, payload, Context.ConnectionAborted).ConfigureAwait(false);
        await Clients.Caller.SendAsync(HubContract.BidAccepted, bid, Context.ConnectionAborted).ConfigureAwait(false);

        if (bid.Extended && bid.NewEndTime.HasValue)
        {
            await Clients.Group(group).SendAsync(HubContract.AuctionExtended, bid.NewEndTime.Value, Context.ConnectionAborted).ConfigureAwait(false);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
        => base.OnDisconnectedAsync(exception);

    private static string AuctionKeyFor(Guid auctionId) => ToysAuctionHouse.Infrastructure.Redis.AuctionKeyBuilder.RoomGroup(auctionId);
}

public sealed record BidRequestDto(Guid AuctionId, decimal Amount, string IdempotencyKey);
