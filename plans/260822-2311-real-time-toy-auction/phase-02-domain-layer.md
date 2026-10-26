# Phase 02 — Domain Layer (Entities & State Machine)

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 01 (skeleton)

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Pending
- Define domain entities, value objects, enums (per spec §5, §4), and the **Auction State Machine**. Domain has zero external dependencies.

## Key Insights
- `AuctionSession.Status` is the business heart: `Scheduled→Active→Ended/Cancelled`. Encode transitions as guarded methods, not free setters.
- `ToyCondition` is a fixed enum (1 NewSeal, 2 SecondHand, 3 Custom); all free-form details live in `Description`.
- Money values are `decimal`. Use `Price` value object to encapsulate `StartPrice/StepPrice/CurrentPrice` + step validation.

## Requirements
- Entities: `User`, `ToyProduct`, `AuctionSession`, `Bid`, `Order`.
- Enums: `ToyCondition(1,2,3)`, `AuctionSessionStatus(1,2,3,4)`, `OrderStatus(1,2,3)`, `AuthProvider(Local,Google,GitHub)`.
- State machine behavior: `CanStart()`, `CanCancel()`, `CanBid()`, `TryMove(To)`, `ApplyAntiSniping(endTime, nowTime)`.
- Rule methods mirror the Lua script (single source of truth where possible): `IsValidBid(amount, step, current)`.

## Files to Create
- `src/ToysAuctionHouse.Domain/Entities/{User,ToyProduct,AuctionSession,Bid,Order}.cs`
- `src/ToysAuctionHouse.Domain/Enums/{ToyCondition,AuctionSessionStatus,OrderStatus,AuthProvider}.cs`
- `src/ToysAuctionHouse.Domain/ValueObjects/Price.cs`, `AuctionWindow.cs`
- `src/ToysAuctionHouse.Domain/StateMachine/AuctionStateMachine.cs` (or as methods on Session)
- `src/ToysAuctionHouse.Domain/Constants/{BidRules,AntiSnipingPolicy}.cs`

## Implementation Steps
1. Define enums with explicit int values matching DB CHECK constraints.
2. Entities as plain POCOs (EF-friendly: public setters for persistence, but wrap state transitions in domain methods).
3. `AuctionSession` exposes:
   - `Activate(now)` → Scheduled→Active (guard).
   - `CancelBy(sellerId, now)` → only if Scheduled.
   - `PlaceBid(bidderId, amount, now)` → validates Active + amount + step; returns accepted/extension result.
   - `Finalize(winnerId, finalAmount, now)` → Active/Ended.
4. Anti-sniping policy constant: `EXTENSION_SECONDS = 120`, `SNIPE_WINDOW_SECONDS = 60`.

## Todo List
- [ ] All entities + enums defined
- [ ] State machine method guards tested

## Success Criteria
- Domain project has NO external package references.
- Unit-testable transition rules; illegal transitions throw/return error.

## Risk Assessment
- Avoid duplicating bid rules in both domain and Lua — keep both, but add a test that asserts they agree (fidelity check).

## Security Considerations
- All transition methods validate caller (e.g., `CancelBy(sellerId)`) to prevent privilege escalation at service layer re-use.
