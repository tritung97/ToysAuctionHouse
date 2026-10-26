# Phase 06 — Application Services

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 04 (Redis), Phase 05 (SQL) — orchestration layer

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Pending
- Implement the Service layer: business logic, DTOs, use-case orchestration, the Phone-number gate, and anti-sniping/order rules. Services call repos only (no Redis/EF directly).

## Key Insights
- Repo+Service style: controllers/Hub → Service → Repos. No MediatR needed (YAGNI).
- Services know *which* store via the repo interface (Redis vs SQL) — the only place that decides.
- Every use case UC-01..UC-13 maps to a service method here.

## Services & Method Map
| Service | Methods | UC |
|---|---|---|
| `AuthService` | `RegisterAsync`, `LoginAsync`, `LoginWithGoogleAsync(idToken)`, `LinkOAuthAsync`, `RefreshAsync` | UC-01,01A,01B |
| `UserService` | `GetProfileAsync`, `RequirePhoneAsync`, `UpdatePhoneAsync` | UC-01C,UC-02 |
| `CatalogService` | `BrowseAsync(filter)`, `GetRoomDetailAsync(id)` | UC-05,UC-06 |
| `AuctionService` | `CreateAsync(sellerId, product, auction)`, `CancelAsync(sellerId, id)`, `JoinRoomAsync`, `PlaceBidAsync(...)`, `LeaveRoomAsync` | UC-03,04,07,08,09 |
| `OrderService` | `ListForUserAsync`, `UpdateSettlementStatusAsync` | UC-12,UC-13 |

## DTOs (under `Application/Dtos`)
- `RegisterDto`, `LoginDto`, `AuthResponseDto`, `OAuthDto`
- `CreateProductDto`, `CreateAuctionDto`, `AuctionRoomDto`, `BidDto`, `BidResultDto`
- `OrderDto`, `CatalogFilterDto`, `PagedResultDto<T>`

## Implementation Steps
1. **Phone gate:** `AuthService`/`AuctionService` checks `PhoneNumber` before `CreateAsync` and `PlaceBidAsync`; if null → `PROFILE_PHONE_REQUIRED` error. (UC-01C)
2. **AuctionService.CreateAsync:** validate `StartTime>=now`, `EndTime>StartTime`, `StartPrice>0`, `StepPrice>0`; single transaction saving product + scheduled session (CurrentPrice=StartPrice). (UC-03)
3. **AuctionService.CancelAsync:** assert `SellerId==caller && Status==Scheduled`; set Cancelled. Else HTTP 400. (UC-04)
4. **CatalogService.BrowseAsync:** validate filters (Category, Condition 1/2/3, Status), paginate via SQL repo. (UC-05)
5. **CatalogService.GetRoomDetailAsync:** read meta+bids from Redis repo; enrich seller/product from SQL; return `AuctionRoomDto`. (UC-06)
6. **AuctionService.PlaceBidAsync:** hot path — call `IRedisBidRepository.PlaceBidAsync` (single Lua), enforce phone gate, map `BidResult` → `BidResultDto`. Broadcast handled in Phase 08 via `IHubContext`; service returns result, hub publishes. (UC-08)
7. **OrderService.UpdateSettlementStatusAsync:** assert caller is Seller of that order and status is Pending; set Completed/Cancelled. (UC-13)

## Todo List
- [ ] All services wired to repos
- [ ] Phone gate blocks bid/create for OAuth users without phone
- [ ] UC mapping table fully covered

## Success Criteria
- Each use case reachable through a service method.
- Business-rule failures return typed results (not exceptions) for clean API mapping.
- Services contain no Redis/EF calls (enforced by interface boundaries).

## Risk Assessment
- Avoid logic duplication with Phase 02 domain rules — services delegate to domain methods where possible against SQL, but the hot path must match the Lua script. Add fidelity unit test.

## Security Considerations
- Ownership checks in every mutating service method (`sellerId==caller`).
- OAuth account link must not allow account takeover on email mismatch — verify provider + subject.
