# Phase 08 — SignalR Hub & Background Automated Jobs

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 06/07 (services wired), Phase 04 (Redis) — real-time core

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 (CRITICAL) | **Status:** Pending
- Implement `AuctionHub` (join/bid/leave, group membership, broadcast) and the `BackgroundService` lifecycle jobs (Activator, Finalizer, BidPersistor, OrderSettler). This bridges Redis hot-state ↔ SQL ledger.

## Key Insights
- **Broadcast NEVER inside Lua.** Lua returns a typed result; the Hub broadcasts `ReceiveNewBid` / `AuctionExtended` after.
- **Reconnect safety:** `JoinAuctionRoom` sends a snapshot (top-10 bids + remaining time) so a dropped socket self-heals (UC-07).
- **Stream consumer group** for the persistor → idempotent `XACK`-based processing.
- Scale-out: if >1 API instance → Redis backplane (`AddSignalR().AddStackExchangeRedis(...)`).

## SignalR Hub: `AuctionHub`
- `[Authorize] JoinAuctionRoom(auctionId)` → `Groups.AddToGroupAsync(ctx.ConnectionId, $"Auction_{auctionId}")`; push snapshot via `Clients.Caller.SendAsync("RoomSnapshot", dto)`.
- `[Authorize] SendBid(BidRequestDto)` → JSON `{ auctionId, amount, idempotencyKey }`.
  1. Call `AuctionService.PlaceBidAsync`.
  2. If accepted → `BroadcastNewBid`; if `extended` → broadcast `AuctionExtended(newEndTime)`.
  3. If rejected → `Clients.Caller.SendAsync("BidRejected", reason)` (duplicate/outbid/ended/phone-required).
- `OnDisconnectedAsync` → remove from groups (UC-09).

## Background Jobs
| Job | Interval | Responsibility |
|---|---|---|
| `AuctionActivatorService` | 5–10s | `Scheduled→Active` when `StartTime<=now`; init Redis keys; add to active_list; broadcast `AuctionStarted`. (UC-10) |
| `AuctionFinalizerService` | 1–2s | Scan active_list for `UtcNow>=EndTime`; Lua-mark Ended; get winner from ZSET; `SqlAuctionRepository.FinalizeAndPersistAsync`; broadcast `AuctionEnded`; cleanup after grace. (UC-11) |
| `BidPersistorService` | continuous | `XREADGROUP` consumer from `auction:bids:stream`; insert into `Bids` (idempotent); `XACK`. (Phase 04 stream) |

**Template Method:** base `PeriodicBackgroundService` handles loop + `CancellationToken`; subclasses override `ExecuteAsync`.

## Files to Create
- `src/ToysAuctionHouse.Api/Hubs/AuctionHub.cs`, `Contracts/IHubContract.cs` (client method names)
- `src/ToysAuctionHouse.Infrastructure/Background/{PeriodicBackgroundService,AuctionActivatorService,AuctionFinalizerService,BidPersistorService}.cs`
- `src/ToysAuctionHouse.Api/Program.cs` — `AddSignalR()`, `AddHostedService<...>()`, `MapHub<AuctionHub>("/hubs/auction")`

## Implementation Steps
1. Base `PeriodicBackgroundService : BackgroundService` with `Interval` + `ExecuteAsync(CancellationToken)` abstract.
2. **Activator:** query `GetScheduledDueAsync(now)` → for each: `InitSessionAsync` (Redis), update `Status=Active` in SQL.
3. **Finalizer:** `GetExpiredActiveAsync(now)` → `LuaMarkEnded` → `GetTopBidAsync` → `FinalizeAndPersistAsync` → broadcast. Grace-delay cleanup via task that `DEL`s keys after retention.
4. **Persistor:** consumer group `AuctionBidPersistors`, `XREADGROUP`, insert idempotent, `XACK`.
5. Hub injects `IHubContext<AuctionHub>` into services (or returns result to hub for broadcast). Prefer: **hub calls service and broadcasts** to keep services pure.

## Todo List
- [ ] Join sends correct snapshot; leave cleans group
- [ ] SendBid broadcasts NewBid + AuctionExtended correctly
- [ ] Activator/Finalizer transition states & persist
- [ ] Persistor idempotent + ACK

## Success Criteria
- UC-07/08/09 flows work end-to-end via WebSocket.
- UC-10/11 finalize auction → Order created exactly once; no duplicate bids in SQL.
- Reconnect re-syncs room state (no missed auction events).

## Risk Assessment
- **Message loss:** mitigated by snapshot-on-join + Stream replay.
- **Finalizer bottleneck** if many auctions end at once → consider a small worker pool.
- **SignalR scale-out** requires Redis backplane — add when >1 instance.

## Security Considerations
- JWT auth on hub; validate `idempotencyKey` per request; never trust client-supplied winner/amounts — derive from Redis.
- Group membership: only authenticated users bid; guests can join read-only (separate `JoinRoomReadOnly`).
