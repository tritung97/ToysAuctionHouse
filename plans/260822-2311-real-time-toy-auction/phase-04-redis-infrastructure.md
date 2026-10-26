# Phase 04 — Redis Infrastructure (Lua, ZSET, Streams)

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 02 (Domain) — the hot-path source of truth

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 (CRITICAL) | **Status:** Pending
- Implement the **atomic bid hot-path** with Redis Lua, plus Room-state repos and the persistence Stream. This is the most important phase — get it right.

## Key Insights
- Redis is single-threaded → a Lua script runs **atomically** (no locks, no race). All bid checks + writes MUST be inside ONE script.
- **Anti-sniping is evaluated inside the script**; only the broadcast happens after (Lua can't call SignalR).
- Persistence queue = Redis **Stream** (not List) → consumer-group ACK, replay, redelivery, idempotent effect.
- This phase owns the "hot store"; SQL repos (Phase 05) only do durable ledger + history.

## Redis Key Schema (canonical)
- `auction:{id}:meta` — HASH: `Status`, `StartPrice`, `StepPrice`, `CurrentPrice`, `CurrentWinnerId`, `StartTime`, `EndTime`, `SellerId`, `ProductId`, `IsAntiSnipingEnabled`, `Version`.
- `auction:{id}:bids` — ZSET: score=amount, member=`{bidderId}:{bidId}:{timestamp}`. For bid history + winner.
- `auction:active_list` — SET of active auction ids (for Finalizer scan).
- `idempotency:{key}` — STRING `PROCESSING` with `EX 30` (SETNX) for replay guard.
- `auction:bids:stream` — STREAM of `BidAccepted` events for the Persistor.

## Files to Create
- `src/ToysAuctionHouse.Infrastructure/Redis/RedisConnectionFactory.cs` (Singleton `ConnectionMultiplexer`)
- `src/ToysAuctionHouse.Infrastructure/Redis/AuctionKeyBuilder.cs`
- `src/ToysAuctionHouse.Infrastructure/Redis/AuctionBidLua.cs` (load+eval script)
- `src/ToysAuctionHouse.Infrastructure/Redis/Repositories/RedisBidRepository.cs` → `IRedisBidRepository`
- `src/ToysAuctionHouse.Infrastructure/Redis/Repositories/RedisAuctionRepository.cs` → `IRedisAuctionRepository`
- `src/ToysAuctionHouse.Infrastructure/Redis/Contracts/IRedisBidRepository.cs`, `IRedisAuctionRepository.cs`

## The Lua Script (pseudocode — keep as loaded string in repo)
```lua
-- KEYS[1]=meta, KEYS[2]=bids zset, KEYS[3]=idempotency key
-- ARGV = amount, bidderId, bidId, now, step, snipeWindow, ext, enabled
if not redis.call('SET', KEYS[3], 'PROCESSING', 'EX', 30, 'NX') then
  return {'DUPLICATE'} end
local meta = redis.call('HGETALL', KEYS[1])
if meta.Status ~= 'Active' or now >= meta.EndTime then return {'EXTENDED'..} end
```
Return shape must expose `accepted, newPrice, newWinnerId, extended?, newEndTime, errorReason`.
**Mock/load-test:** assert a high-concurrency burst never over-accepts or double-persists.

## `RedisBidRepository.PlaceBidAsync`
1. Evaluate script with correct KEYS/ARGV.
2. Map result to `BidResult` (reason enum: `Accepted, Duplicate, Outbid, Ended, NotStarted`).
3. If accepted → `XADD auction:bids:stream` with the bid event (write-ahead before broadcast).
4. Return results.

## `RedisAuctionRepository`
- `GetMetaAsync(id)` → `HGETALL`.
- `GetTopBidsAsync(id, n=10)` → `ZREVRANGE ... WITHSCORES`.
- `GetExpiredActiveAsync(now)` → iterate `auction:active_list` union meta EndTime.
- `InitSessionAsync(session)` → write meta + empty ZSET + add to active_list (called by Activator).
- `MarkEndedAsync(id)` → set meta Status=Ended (atomic via Lua or HSet).
- `CleanupAsync(id)` → DEL keys after grace period.

## Todo List
- [ ] Lua script passes concurrency + idempotency assertions
- [ ] Snapshots return correct top-N + remaining time
- [ ] Stream ACKs & replay work in integration test

## Success Criteria
- A single `PlaceBidAsync` call runs the full check+write atomically and returns a typed reason.
- k6 (Phase 10) proves SQL is never locked during a bid storm.
- Duplicate `idempotencyKey` within TTL → `Duplicate`, no side effects.

## Risk Assessment
- **Hot-key:** one auction = one ZSET key → single Redis node throughput limit. Accept for toy scale; revisit if a single auction exceeds ~1k bid/s.
- **Durability gap:** enable Redis AOF (`appendfsync everysec`) so Stream + meta survive restart.
- **Versioning:** use meta `Version` + optimistic concurrency if stale low-bid must not overwrite high-bid.

## Security Considerations
- Never accept amounts with non-finite/precision-overflow values; validate in script and service.
- Idempotency keys are user-supplied — always namespace (`idempotency:{key}`) to avoid cross-user collisions.
