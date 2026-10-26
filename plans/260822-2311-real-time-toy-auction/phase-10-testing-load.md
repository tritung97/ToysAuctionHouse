# Phase 10 — Testing & Load (k6)

> **Context:** [plan.md](./plan.md) · Dependencies: everything (verification phase)

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Complete
- Unit, integration, and **k6 load** tests. Primary goal of k6: prove SQL is **never locked** under concurrent bid storms, and that the Lua atomic path behaves under load.

## Key Insights
- The architecture's whole value = "bid wars without locking SQL." k6 must verify that assertion.
- Two test tiers: correctness (unit/integration) and load (k6 WebSocket storm).

## Test Matrix
| Type | Target | Assert |
|---|---|---|
| Unit | Domain state machine, anti-sniping | transitions legal/illegal; extension window |
| Unit | Lua fidelity | domain rules == Lua results for sample bids |
| Integration | RedisBidRepository | duplicate idempotency → `Duplicate`, no side effect; concurrent bids → single monotonic winner |
| Integration | FinalizeAndPersist | exactly one Order; no duplicate bids on retry |
| Integration | Hub | join → snapshot; bid → broadcast; extension; end |
| Load (k6) | `SendBid` storm on one auction | no SQL lock/contention; p95 latency, error rate, monotonic price |

## Files to Create
- `tests/ToysAuctionHouse.UnitTests/...` (xUnit) — state machine, price/step, anti-sniping, Lua fidelity
- `tests/ToysAuctionHouse.IntegrationTests/...` — repos, hub, finalizer (Testcontainers or Docker infra)
- `k6/bid-flow.js` — connect via WebSocket, join room, send N bids with unique UUIDs, assert accepted/duplicate ordering

## Implementation Steps
1. Unit tests for Phase 02 + Lua-fidelity check (load script + domain method, compare).
2. Integration tests spin SQL + Redis (docker-compose up / Testcontainers).
3. `k6/bid-flow.js`:
   - Warmup: 1 socket.
   - Ramp: 50→1000 concurrent sockets joining one `AuctionId`.
   - Each sends bids with fresh UUID; assert price strictly increases; assert no SQL write per bid (verify via SQL activity log / counter).
   - Scenarios: duplicate idempotency rejection; outbid rejection.
4. Run `k6 run --vus 1000 --duration 2m k6/bid-flow.js`.
5. Record baseline thresholds; add CI gate (optional).

## Todo List
- [x] Unit tests green
- [x] Integration tests green against docker infra
- [x] k6 run: SQL stays unlocked; latency acceptable
- [x] Thresholds documented

## Success Criteria
- Load test: 0 SQL lock waits; p95 bid latency within target (e.g. < 150ms Redis round-trip).
- No accepted bid lost; idempotency holds under duplicates.

## Risk Assessment
- WebSocket reconnect storms in load test can mimic real spam — capture as signal, not failure.
- Flaky tests if infra shared — isolate per test run.

## Security Considerations
- Load tests must use test data only; never hit prod Redis/SQL with real items.
