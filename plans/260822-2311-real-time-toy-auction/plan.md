---
title: "Real-Time Collectible Toy Auction Platform"
description: "Greenfield .NET 8 + React real-time auction platform using Redis hot-path + SQL ledger, SignalR, and Repository/Service architecture."
status: complete
priority: P1
effort: 90h
branch: main
tags: [feature, backend, frontend, database, api, realtime]
created: 2026-08-22
---

# Real-Time Collectible Toy Auction Platform

## Overview

Build a real-time online auction platform for collectible toys (Gunpla, SH.Figuarts, Hot Toys, custom kits). High-concurrency, time-critical bidding without locking SQL. Architecture: **Modular Monolith + Clean Architecture**, **Redis = hot store** (Lua atomic bids, ZSET room state, Streams for persistence), **SQL Server = durable ledger**, **SignalR** for real-time, **Repository/Service** layers (no CQRS).

## Phases

| # | Phase | Status | Effort | Link |
|---|-------|--------|--------|------|
| 1 | Solution Scaffold & Environment | Complete | 6h | [phase-01](./phase-01-solution-scaffold.md) |
| 2 | Domain Layer (State Machine) | Complete | 8h | [phase-02-domain-layer.md](./phase-02-domain-layer.md) |
| 3 | Database Layer (EF Core) | Complete | 8h | [phase-03-database-layer.md](./phase-03-database-layer.md) |
| 4 | Redis Infrastructure (Lua/Streams) | Complete | 12h | [phase-04-redis-infrastructure.md](./phase-04-redis-infrastructure.md) |
| 5 | SQL Repositories (Cache-Aside) | Complete | 6h | [phase-05-sql-repositories.md](./phase-05-sql-repositories.md) |
| 6 | Application Services | Complete | 12h | [phase-06-application-services.md](./phase-06-application-services.md) |
| 7 | API Controllers & Auth | Complete | 10h | [phase-07-api-controllers-auth.md](./phase-07-api-controllers-auth.md) |
| 8 | SignalR Hub & Background Jobs | Complete | 14h | [phase-08-signalr-background.md](./phase-08-signalr-background.md) |
| 9 | React Frontend | Complete | 10h | [phase-09-react-frontend.md](./phase-09-react-frontend.md) |
| 10 | Testing & Load (k6) | Complete | 4h | [phase-10-testing-load.md](./phase-10-testing-load.md) |

## Dependencies

- .NET 8 SDK, Node.js 20+, SQL Server (LocalDB/Docker), Redis (Docker or WSL).
- NuGet: EF Core, Microsoft.Extensions.Caching.StackExchangeRedis, StackExchange.Redis, SignalR, JWT Bearer, BCrypt.Net-Next, MediatR (optional, not required).
- npm: React 18, TypeScript, TailwindCSS, @microsoft/signalr, react-router, TanStack Query.
- k6 for load testing.

## Key Architecture Rules

1. **Hot path never locks SQL.** `SendBid` = one atomic Redis Lua script.
2. **Redis = source-of-truth while live.** SQL = durable ledger.
3. **Persistence is async** via Redis Streams → BackgroundService Finalizer.
4. **Idempotency** enforced in Redis (`SET NX EX 30`) AND SQL (`UNIQUE` index).
5. **Anti-sniping** extends `EndTime` by 120s if bid within 60s of end — evaluated inside Lua.
6. Repo boundary abstracts store choice; Service layer never touches Redis/EF directly.
7. Broker-less: SignalR Hub + Redis backplane for scale-out; no RabbitMQ/Kafka (YAGNI).

## Definitions of Done (global)

- Every use case UC-01..UC-13 maps to a service method + repo method (see phase files).
- All writes on live auctions via Redis Lua; all finalization via a single SQL transaction.
- Unit tests for state machine + Lua logic; k6 verifies SQL is never locked under concurrent bids.
- `/clear` then `/cook --auto {abs plan path}/plan.md` to execute.
