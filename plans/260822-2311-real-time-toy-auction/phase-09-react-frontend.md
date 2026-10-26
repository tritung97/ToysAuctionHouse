# Phase 09 — React Frontend

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 07 (API), Phase 08 (Hub) — client for everything above

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Complete
- Build the React (TypeScript + TailwindCSS) SPA: auth, catalog, live auction room, orders. Custom hooks manage the SignalR lifecycle (connect/reconnect/group join).

## Key Insights
- **SignalR lifecycle belongs in custom hooks** (spec §2): `useAuctionHub` handles connection, auto-reconnect, join/leave, and event subscriptions.
- **Countdown + anti-sniping:** timers update from `AuctionExtended` events and `RoomSnapshot`.
- Client generates `idempotencyKey` (UUID) **on button press** per bid — one per bid attempt.
- Data via TanStack Query for REST; SignalR events for live mutations.

## Pages / Routes
- `/` catalog browse (UC-05) · `/login`, `/register` (UC-01) · `/auction/:id` room (UC-06/07/08/09)
- `/orders` (UC-12) · `/sell` create listing (UC-03) · `/me` profile + phone gate (UC-01C/UC-02)

## Files to Create
- `src/web/src/lib/api.ts` (axios/fetch + JWT interceptor + refresh)
- `src/web/src/lib/signalr/useAuctionHub.ts` (connection lifecycle, group join, event map, reconnect)
- `src/web/src/hooks/useAuctionSocket.ts`, `useCountdown.ts`, `useIdempotency.ts`
- `src/web/src/context/AuthContext.tsx`, `src/web/src/router.tsx`
- `src/web/src/pages/{CatalogPage,LoginPage,RegisterPage,AuctionRoomPage,OrdersPage,SellPage,ProfilePage}.tsx`
- `src/web/src/components/{AuctionCard,BidFeed,CountdownTimer,BidButton,PhoneRequiredModal}.tsx`

## Implementation Steps
1. **Auth:** store access+refresh token (memory/localStorage), interceptor auto-refresh; OAuth via Google button.
2. **Catalog:** TanStack Query `useQuery(['catalog', filters])`; filter bar (Category, Condition, Status); pagination.
3. **Room:** `useAuctionHub` join on mount → `RoomSnapshot` initializes state; subscribe `ReceiveNewBid`, `AuctionExtended`, `AuctionEnded`. `BidButton` generates UUID → `SendBid` → handle `BidRejected`.
4. **Countdown:** derive remaining from `RoomSnapshot`/`AuctionExtended`; show "extended" toast.
5. **Phone gate:** on bid/sell attempt, if `PROFILE_PHONE_REQUIRED` → show `PhoneRequiredModal` → update phone → retry.
6. **Orders:** list with counterparty name/phone; seller mark completed/cancelled.
7. Tailwind styling: card grid, live badge, countdown, bid feed.

## Todo List
- [x] Auth + token refresh works
- [x] Room joins + live bids + extension + end update UI
- [x] Phone gate modal blocks new users from bidding
- [x] Catalog/orders pages consume APIs

## Success Criteria
- End-to-end bid flow: join → bid → live feed refresh → extension countdown → winner banner.
- Reconnect keeps room in sync.
- No UI action on a cancelled/ended auction.

## Risk Assessment
- SignalR reconnect should re-emit `JoinAuctionRoom` (snapshot re-sync).
- Avoid leaking tokens; use memory storage + refresh flow.
- Preserve `idempotencyKey` deduplication: disable bid button after press until response.

## Security Considerations
- Never log tokens; validate Google `id_token` client-side only as UX — real validation is server-side.
- XSS: escape user-generated content (names, bidder names) in feed.
