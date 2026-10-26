# Phase 01 — Solution Scaffold & Environment

> **Context:** [plan.md](./plan.md) · Dependencies: none (start of project)

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Pending
- Scaffold the .NET solution and React app, wire infra (SQL Server, Redis), and establish the folder structure. Nothing functional — just buildable skeleton + config.

## Requirements
- .NET 8 solution with Clean Architecture project separation.
- React + TypeScript app scaffolded with TailwindCSS + SignalR client + TanStack Query.
- Config (appsettings) pointing to SQL Server + Redis; docker-compose for infra.
- `.gitignore`, README, and solution-level tooling (dotnet-ef, k6).

## Architecture
- Solution layout (see plan.md). Projects depend downward only:
  Domain ← Application ← (Infrastructure, Api)
- Layer boundaries: Services (Application) → Repositories (Infrastructure). Domain = no external deps.

## Files to Create
- `src/ToysAuctionHouse.Domain/ToysAuctionHouse.Domain.csproj` (classlib, no external refs)
- `src/ToysAuctionHouse.Application/...` (refs Domain)
- `src/ToysAuctionHouse.Infrastructure/...` (refs Application; EF, Redis, SignalR)
- `src/ToysAuctionHouse.Api/...` (refs Infrastructure+Application; Controllers, Hubs, Program.cs)
- `tests/ToysAuctionHouse.UnitTests/...`, `tests/ToysAuctionHouse.IntegrationTests/...`
- `src/web/` — Vite React TS app (`npm create vite@latest web -- --template react-ts`)
- `docker-compose.yml` (mssql + redis), `appsettings.json` / `appsettings.Development.json`
- `.gitignore`, `README.md`, `k6/bid-flow.js` (empty stub now)

## Implementation Steps
1. `dotnet new sln -n ToysAuctionHouse`.
2. Create classlib/wepapi projects above; add them to sln; set project references.
3. Verify API project builds (`dotnet build`).
4. `docker-compose up -d mssql redis`; verify connectivity strings.
5. Scaffold React app; install `tailwindcss @microsoft/signalr @tanstack/react-query react-router-dom`.
6. Configure CORS + SignalR route placeholder in `Program.cs`.
7. Add VS Code tasks / launch configs.

## Todo List
- [ ] Solution + projects build cleanly
- [ ] docker-compose infra runs locally
- [ ] React dev server serves Tailwind-styled hello page

## Success Criteria
- `dotnet build` passes across all projects.
- `docker compose up` starts SQL + Redis; API connects to both.
- React app loads with Tailwind utilities working.

## Risk Assessment
- Port/LocalDB conflicts on Windows — use Docker for determinism.
- Solution naming/namespace drift — standardize `ToysAuctionHouse.{Layer}`.

## Security Considerations
- Keep connection strings in User-Secrets / env vars, never in committed appsettings for prod.
