# Real-Time Collectible Toy Auction Platform

A greenfield high-concurrency auction platform for collectible toys (Gunpla, S.H.Figuarts, Hot Toys, custom kits).

**Architecture:** Modular Monolith + Clean Architecture. Redis carries the hot store, SQL Server is the durable ledger, SignalR streams real-time events.

## Tech Stack

- **Backend:** .NET 8 / ASP.NET Core Web API (Clean Architecture)
- **Real-Time:** SignalR Hub (WebSocket, auto-reconnect)
- **Hot Store:** Redis (ZSET, Hash, Lua scripts, Streams)
- **Durable Ledger:** SQL Server + EF Core
- **Frontend:** React 18 + TypeScript + TailwindCSS + TanStack Query + SignalR client
- **Load Testing:** k6

## Project Layout

```
src/
  ToysAuctionHouse.Domain         (no external deps, entities + state machine)
  ToysAuctionHouse.Application    (services, DTOs — refs Domain)
  ToysAuctionHouse.Infrastructure (EF, Redis, repos, background jobs — refs Application)
  ToysAuctionHouse.Api            (Controllers, Hubs, Program.cs — refs Infra + App)
  web/                            (Vite React TS app)
tests/
  ToysAuctionHouse.UnitTests
  ToysAuctionHouse.IntegrationTests
```

## Getting Started

### 1. Seed infrastructure

```bash
docker compose up -d mssql redis
```

### 2. Build backend

```bash
dotnet build
```

### 3. Apply migrations + seed data

```bash
dotnet ef database update --project src/ToysAuctionHouse.Infrastructure --startup-project src/ToysAuctionHouse.Api
```

### 4. Run API

```bash
dotnet run --project src/ToysAuctionHouse.Api
```

### 5. Run React app

```bash
cd src/web
npm install
npm run dev
```

## Key Architecture Rules

1. The bid hot-path is a **single atomic Redis Lua script** — SQL is never locked during a bid.
2. Redis is the source of truth while an auction is live; SQL is the durable ledger.
3. Persistence is async (Redis Streams → BackgroundService finalizer).
4. Idempotency enforced in Redis (`SET NX EX 30`) **and** SQL (UNIQUE index).
5. Anti-sniping extends `EndTime` by 120s when a bid lands within 60s of the end.
6. Repo boundary abstracts the store; the service layer never touches Redis/EF directly.

## Load / Integration Testing

See `k6/bid-flow.js`. Run with `k6 run k6/bid-flow.js`.
