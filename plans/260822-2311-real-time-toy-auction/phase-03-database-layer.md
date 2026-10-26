# Phase 03 — Database Layer (EF Core)

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 02 (Domain)

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Pending
- Persist domain to SQL Server via EF Core: `DbContext`, entity configs, migrations, indexes (spec §5.2, §5.3).

## Key Insights
- EF entity config documents the spec tables exactly: `Users`, `ToyProducts`, `AuctionSessions`, `Bids`, `Orders`.
- Indexes from spec §5.3 are critical for the BackgroundService polling (`IX_AuctionSessions_Status_EndTime`) and catalog filters.
- `Bids.IdempotencyKey` UNIQUE index = hard idempotency guarantee behind the Redis check.
- `ToyProduct.ImageUrls` stored as JSON string (NVARCHAR(MAX)). Serialize as `List<string>`.

## Requirements
- DbContext `AuctionDbContext` with 5 DbSets.
- Fluent API configs: keys, FKs, uniquing, CHECK constraints, decimal precision (18,2), datetime UTC.
- Migrations created via `dotnet ef migrations add`.
- Initial seed data (a few users, products, one scheduled + one active auction).

## Files to Create / Modify
- `src/ToysAuctionHouse.Infrastructure/Persistence/AuctionDbContext.cs`
- `src/ToysAuctionHouse.Infrastructure/Persistence/Configurations/{User,ToyProduct,AuctionSession,Bid,Order}Configuration.cs`
- `src/ToysAuctionHouse.Infrastructure/Persistence/SeedData.cs`
- `appsettings.json` connection string (`SqlServer`)

## Implementation Steps
1. Register `AddDbContext<AuctionDbContext>` in DI (Infrastructure `AddInfrastructure` extension).
2. Configurations:
   - `AuctionSession.ProductId` UNIQUE (1-1), `Order.AuctionSessionId` UNIQUE (1-1).
   - `User.Username` / `Email` UNIQUE.
   - `SessionStatus` and `OrderStatus` TINYINT with CHECK `IN (1,2,3,4)` / `IN (1,2,3)`.
   - `Condition` TINYINT CHECK `IN (1,2,3)`.
   - All decimal `HasPrecision(18,2)`.
3. Create the 3 indexes from spec §5.3.
4. `dotnet ef migrations add InitialCreate`; `dotnet ef database update`.
5. Seed: create 2 users (buyer/seller), 2 products, a scheduled and an active session.

## Todo List
- [ ] Migrations apply cleanly; tables match spec
- [ ] Indexes present (`IX_...`)
- [ ] Seed data loads

## Success Criteria
- `dotnet ef database update` succeeds; schema matches spec §5.2.
- Query plan uses `IX_AuctionSessions_Status_EndTime` when filtering Status/EndTime.

## Risk Assessment
- UTC consistency: always write `DateTime.UtcNow`. Configure `UseUtc`/DateTime kind to avoid local time drift.
- Avoid locking: these migration/`Update` calls are background-path only; live bids never hit this DB.

## Security Considerations
- EF queries parameterized → SQL injection-safe. Keep connection string secret.
