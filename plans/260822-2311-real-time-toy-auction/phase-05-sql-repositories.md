# Phase 05 — SQL Repositories (EF + Cache-Aside)

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 03 (DB), Phase 04 (Redis for cache-aside reads)

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Pending
- Implement SQL-backed repositories (durable ledger + history) and Cache-Aside for catalog/product reads. These are **not** the bid hot path — they serve reads and finalization.

## Key Insights
- Two repo groups: **hot (Redis)** live in Phase 04; **durable (SQL)** live here. Service orchestrates both.
- Cache-Aside on catalog/detail reads: Redis check → miss → SQL → populate with TTL. Cheap wins; don't overthink.
- Auction finalization writes belong to a SQL repo method owning one `DbContext` transaction.

## Repositories to Implement
| Repo | Fills | Notes |
|---|---|---|
| `ISqlUserRepository` | Auth/profile/contact | find by email/provider, save phone number |
| `ISqlProductRepository` | Catalog + Cache-Aside | list/filter/detail |
| `ISqlAuctionRepository` | Sessions/history/finalize | `FinalizeAndPersistAsync` owns UoW |
| `ISqlOrderRepository` | Settlement orders | create/list/update status |

## Files to Create
- `src/ToysAuctionHouse.Infrastructure/Persistence/Repositories/{SqlUserRepository,SqlProductRepository,SqlAuctionRepository,SqlOrderRepository}.cs`
- Matching interfaces in `Infrastructure/Persistence/Repositories/Contracts/`
- `src/ToysAuctionHouse.Infrastructure/Caching/CacheAsideService.cs` (wrapper around `IDistributedCache`)

## Implementation Steps
1. Each repo takes `AuctionDbContext` (scoped) — or `IDbContextFactory` for background workers.
2. `SqlProductRepository`:
   - `GetPagedAsync(filter, page, pageSize)` → uses indexes `IX_ToyProducts_Category_Condition`.
   - `GetByIdAsync(id)` → Cache-Aside (key `catalog:product:{id}`, TTL 10min).
3. `SqlAuctionRepository`:
   - `GetScheduledDueAsync(now)` → `Status==Scheduled && StartTime<=now`.
   - `FinalizeAndPersistAsync(auctionId, winnerBidDto)` → **one transaction**: update session (Ended/CurrentPrice/WinnerId), bulk-insert bids (idempotent on `IdempotencyKey`), create `Order` if winner. Returns order id.
4. `SqlOrderRepository`: create order, `GetByParticipantAsync(userId)`, `UpdateStatusAsync(orderId, status, sellerId)`.
5. `SqlUserRepository`: `GetByEmailAsync`, `GetByOAuthAsync(provider,key)`, `CreateAsync`, `UpdatePhoneAsync`.

## Todo List
- [ ] All SQL repos implemented against EF
- [ ] FinalizeAndPersist is transactional + idempotent
- [ ] Cache-Aside works on product detail

## Success Criteria
- Finalize writes session + bids + order atomically; a retry does not duplicate bids.
- Catalog page query hits the spec indexes.

## Risk Assessment
- **Long transactions:** keep SQL transactions short; never inside the hot path.
- **Stale cache:** invalidate `catalog:product:{id}` on product update; short TTL mitigates.

## Security Considerations
- Repos always scope reads by user/ownership where applicable (e.g., order list only for participant).
