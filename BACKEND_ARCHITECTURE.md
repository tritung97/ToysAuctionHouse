# Backend Code Guide — Real-Time Collectible Toy Auction

This document explains **what each backend file and folder does** and **why it exists**.
It is a map of the .NET implementation (phases 01–08) so a new developer can navigate
the codebase quickly.

---

## 1. Architecture at a glance

**Clean Architecture, Modular Monolith.** The backend is one deployable ASP.NET Core
API with four logical layers, where dependencies point *downward only*:

```
┌────────────────────────────────────────────────────────────┐
│  ToysAuctionHouse.Api          (web host, controllers, hubs)│
│  depends on → Application + Infrastructure                 │
├────────────────────────────────────────────────────────────┤
│  ToysAuctionHouse.Infrastructure  (EF, Redis, repos, jobs)  │
│  depends on → Application          (implements the ports)  │
├────────────────────────────────────────────────────────────┤
│  ToysAuctionHouse.Application       (services, DTOs, ports) │
│  depends on → Domain                                       │
├────────────────────────────────────────────────────────────┤
│  ToysAuctionHouse.Domain            (entities, pure rules)  │
│  depends on → nothing               (zero external refs)   │
└────────────────────────────────────────────────────────────┘
```

### The three stores
| Store | Role |
|---|---|
| **Redis** | Source of truth while an auction is **live** (hot path). Holds meta hash, bid ZSET, active-list, idempotency keys, and a persistence Stream. |
| **SQL Server** | Durable **ledger** + history: users, products, sessions, bids, orders. Written asynchronously, never locked during a bid. |
| **SignalR** | Real-time push to clients (new bid, extension, started, ended, snapshot). |

### Key rule
The bid hot path runs as **one atomic Redis Lua script**. SQL is only touched by
background jobs and read paths — never inside bid processing.

---

## 2. Solution layout

```
ToysAuctionHouse.sln
├─ .config/dotnet-tools.json        local dev tools (dotnet-ef)
├─ docker-compose.yml               SQL Server + Redis infra
├─ k6/bid-flow.js                   load test stub (Phase 10)
├─ src/
│  ├─ ToysAuctionHouse.Domain/
│  ├─ ToysAuctionHouse.Application/
│  ├─ ToysAuctionHouse.Infrastructure/
│  ├─ ToysAuctionHouse.Api/
│  └─ web/                          (React frontend, excluded from this guide)
└─ tests/
   ├─ ToysAuctionHouse.UnitTests/
   └─ ToysAuctionHouse.IntegrationTests/
```

---

## 3. Domain — `src/ToysAuctionHouse.Domain`

Pure business logic. **No NuGet packages, no I/O.** Everything here is testable in
isolation.

### `Entities/`
| File | Purpose |
|---|---|
| `User.cs` | Account + contact for buyers/sellers. Holds `PasswordHash` (null for OAuth) and `HasPhoneNumber()` helper used by the phone gate. |
| `ToyProduct.cs` | The collectible item. `ImageUrls` is stored as a JSON string. `Condition` is the fixed 1/2/3 enum. |
| `AuctionSession.cs` | The business heart. Owns the price/step/timeline and **all state-transition methods**: `Activate`, `CancelBy`, `PlaceBid`, `Finalize`. Returns a `BidAcceptance` record describing outcome + anti-sniping extension. |
| `Bid.cs` | One accepted bid. `IdempotencyKey` is the client UUID (uniquely deduped). |
| `Order.cs` | Settlement order created when an auction ends: buyer/seller + final amount + status. |

### `Enums/`
| File | Value | Notes |
|---|---|---|
| `ToyCondition.cs` | 1,2,3 | NewSeal / SecondHand / Custom |
| `AuctionSessionStatus.cs` | 1,2,3,4 | Scheduled / Active / Ended / Cancelled |
| `OrderStatus.cs` | 1,2,3 | Pending / Completed / Cancelled |
| `AuthProvider.cs` | 1,2,3 | Local / Google / GitHub |

All values must match the SQL `CHECK` constraints and the `TINYINT` stores.

### `ValueObjects/`
| File | Purpose |
|---|---|
| `Price.cs` | Wraps a `decimal` amount, guarantees non-negative + 2-dp precision, and implements `IsValidBid(amount, step, current)` — the same rule the Lua script uses. |
| `AuctionWindow.cs` | Encapsulates start/end time, validates `EndTime > StartTime`, and exposes `IsWithinSnipeWindow(...)` used by anti-sniping. |

### `StateMachine/`
| File | Purpose |
|---|---|
| `AuctionStateMachine.cs` | Pure, side-effect-free transition rules. `CanActivate/CanCancel/CanBid/CanFinalize`, `ValidateBid(...)`, and `ApplyAntiSniping(...)`. This is the **single source of truth** that is mirrored (and fidelity-tested) against the Redis Lua script. |

### `Constants/`
| File | Purpose |
|---|---|
| `BidRules.cs` | `MinimumStartPrice`, `MinimumStepPrice`, precision scale. |
| `AntiSnipingPolicy.cs` | `ExtensionSeconds = 120`, `SnipeWindowSeconds = 60`. |

---

## 4. Application — `src/ToysAuctionHouse.Application`

Orchestration layer: DTOs, services, and the **ports (interfaces)** that Infrastructure
implements. Services never touch Redis/EF directly — they call interfaces.

### `Abstractions/`
| File | Purpose |
|---|---|
| `Repositories/IRedisBidRepository.cs` | The **bid hot-path port**. `PlaceBidAsync` (runs the Lua script) + Stream write/read/ACK for persistence. Defines `BidStreamMessage`. |
| `Repositories/IRedisAuctionRepository.cs` | Port for Redis room state: init meta, read snapshot, top-N bids, winning bid, expired-active scan, mark-ended, cleanup. |
| `Repositories/ISqlUserRepository.cs` | Durable user queries: by id/email/OAuth, uniqueness checks, create/update, phone update. |
| `Repositories/ISqlProductRepository.cs` | Catalog + cache-aside (`GetPagedAsync`, `GetByIdAsync`), product create + cache invalidation. |
| `Repositories/ISqlAuctionRepository.cs` | Session queries, scheduled-due scan, create, status update, idempotent bid persist, and the transactional `FinalizeAndPersistAsync`. |
| `Repositories/ISqlOrderRepository.cs` | Order list by participant, get, create, seller-owned status update. |
| `BidResult.cs` | `BidStatus` enum + `BidResult` record returned by the Redis bid repo across the layer boundary. |
| `AuctionSnapshot.cs` | Read-model of the Redis meta hash (`AuctionSnapshot`) + `BidEntry` for ZSET rows. |
| `FinalizeCommand.cs` | `BidPersistItem` + `FinalizeCommand` — everything the finalizer hands to one SQL transaction. |
| `AuthServices.cs` | Ports `IAuthTokenService` (create/validate JWT) and `IPasswordHasher` (BCrypt), plus `GeneratedTokens` and `GoogleIdentity` records. |
| `IAuctionNotifier.cs` | Abstraction for pushing real-time events to a SignalR group. Implemented in the API so Infrastructure never references the Hub. |

### `Common/`
| File | Purpose |
|---|---|
| `ServiceResult.cs` | Non-generic + generic `ServiceResult<T>` with a `ServiceErrorCode` enum. Services return these instead of throwing, so controllers map cleanly to HTTP codes. |

### `Dtos/`
| File | Purpose |
|---|---|
| `AuthDtos.cs` | `RegisterDto`, `LoginDto`, `OAuthDto`, `AuthResponseDto`, `UserProfileDto`. |
| `AuctionDtos.cs` | `CreateProductDto`, `CreateAuctionDto`, `CreateAuctionRequest`, `BidDto`, `AuctionRoomDto` (room snapshot), `BidResultDto`. |
| `CatalogFilterDto.cs` | Query filter for `GET /api/catalog` (category/condition/status/page). |
| `ProductDto.cs` | `ProductCardDto` (catalog list) and `ProductDetailDto` (cache-aside product detail). |
| `OrderDto.cs` | Order projection with counterparty name + phone. |
| `PagedResultDto.cs` | Generic paginated envelope used by catalog. |

### `Services/`
| File | Purpose |
|---|---|
| `AuthService.cs` | `Register`, `Login`, `LoginWithGoogleAsync` (upsert/link/create flow), `Refresh`. Enforces unique email/username. |
| `UserService.cs` | `GetProfileAsync`, `UpdatePhoneAsync` (satisfies the phone gate). |
| `CatalogService.cs` | `BrowseAsync` (SQL paged catalog) and `GetRoomDetailAsync` — reads Redis for live room, enriches seller/product from SQL. |
| `AuctionService.cs` | `CreateAsync` (product + scheduled session, validates schedule, phone gate), `CancelAsync` (ownership + Scheduled-only), `PlaceBidAsync` (the **hot path** — calls Redis Lua, enforces phone gate, maps to `BidResultDto`). |
| `OrderService.cs` | `ListForUserAsync`, `UpdateSettlementStatusAsync` (seller-owned, Pending-only). |

### `DependencyInjection.cs`
Registers all services with `AddApplication(...)`. Called from `Program.cs`.

---

## 5. Infrastructure — `src/ToysAuctionHouse.Infrastructure`

Implements the Application ports. This is where EF Core, Redis, and the background
workers live.

### `Persistence/`
| File | Purpose |
|---|---|
| `AuctionDbContext.cs` | EF Core `DbContext` with the 5 `DbSet`s. Applies all entity configurations. |
| `AuctionDbContextFactory.cs` | Design-time factory for `dotnet ef` migrations — builds a context from a connection string without booting the host. |
| `Configurations/UserConfiguration.cs` | Maps `User`: unique username/email, nullable phone/password for OAuth, auth provider TINYINT, relationships. |
| `Configurations/ToyProductConfiguration.cs` | Maps `ToyProduct`: index on `(Category, Condition)`, `Condition` CHECK (1,2,3), `ImageUrls` JSON converter + comparer. |
| `Configurations/AuctionSessionConfiguration.cs` | Maps `AuctionSession`: unique `ProductId` (1-1), `decimal(18,2)`, status CHECK (1,2,3,4), index `IX_AuctionSessions_Status_EndTime` (Include CurrentPrice, WinnerId), anti-sniping default. |
| `Configurations/BidConfiguration.cs` | Maps `Bid`: `BIGINT` PK, unique `IdempotencyKey`, index `IX_Bids_AuctionSessionId_Amount` (Amount DESC). |
| `Configurations/OrderConfiguration.cs` | Maps `Order`: unique `AuctionSessionId` (1-1), status CHECK (1,2,3). |
| `SeedData.cs` | Idempotent dev seed: 2 users, 2 products, 1 scheduled + 1 live auction. Invoked on startup. |
| `Migrations/*` | Generated `InitialCreate` migration + model snapshot. Apply with `dotnet ef database update`. |

### `Persistence/Repositories/`
| File | Purpose |
|---|---|
| `SqlUserRepository.cs` | Implements `ISqlUserRepository` against EF. |
| `SqlProductRepository.cs` | Implements `ISqlProductRepository`. `GetByIdAsync` is **cache-aside** (Redis → miss → SQL → populate TTL 10 min). |
| `SqlAuctionRepository.cs` | Implements `ISqlAuctionRepository`. `PersistBidsAsync` (idempotent insert) and `FinalizeAndPersistAsync` (one transaction: finalize session → insert bids → create order). |
| `SqlOrderRepository.cs` | Implements `ISqlOrderRepository`. |

### `Redis/`
| File | Purpose |
|---|---|
| `RedisConnectionFactory.cs` | Singleton `ConnectionMultiplexer` from config; provides `IDatabase`. |
| `AuctionKeyBuilder.cs` | Canonical key schema (`auction:{id}:meta`, `:bids`, `auction:active_list`, `auction:bids:stream`, `idempotency:{key}`, `Auction_{id}` group). Prevents string drift. |
| `AuctionBidLua.cs` | The **atomic Lua bid script**. KEYS/ARGV documented in-code. Performs idempotency SET NX, state/amount guards, ZADD, anti-sniping extension, returns a typed array. |
| `RedisBidRepository.cs` | Implements `IRedisBidRepository`: evaluates the Lua, maps to `BidResult`, XADDs accepted bids to the stream, consumer-group read + ACK. |
| `RedisAuctionRepository.cs` | Implements `IRedisAuctionRepository`: meta hash read/write, ZSET top-N + winning bid, expired-active scan, mark-ended, cleanup. |

### `Caching/`
| File | Purpose |
|---|---|
| `CacheAsideService.cs` | Thin wrapper over `IDistributedCache` (JSON get/set/remove), used by product cache-aside. |

### `Background/`
| File | Purpose |
|---|---|
| `PeriodicBackgroundService.cs` | Base class: loop + `CancellationToken` + error handling; subclasses override `ExecuteTickAsync`. |
| `AuctionActivatorService.cs` | Every ~5s. Fetches Scheduled-due sessions → sets `Active` → `InitSessionAsync` (Redis) → broadcasts `AuctionStarted`. |
| `AuctionFinalizerService.cs` | Every ~1s. Scans Redis active-list for expired auctions → marks Ended → gets winning bid → `FinalizeAndPersistAsync` (SQL tx + order) → broadcasts `AuctionEnded` → cleanup after grace. |
| `BidPersistorService.cs` | Continuous. Drains the bid stream (consumer group), idempotently persists bids to SQL, then `XACK`s. |

### `DependencyInjection.cs`
`AddInfrastructure(...)`: registers EF `DbContext`, Redis cache + singleton connection/repos, scoped SQL repos, cache-aside, and the 3 hosted services.

---

## 6. API — `src/ToysAuctionHouse.Api`

The HTTP/WebSocket host. Maps use cases to endpoints, owns auth (JWT/OAuth/BCrypt),
and does all signal broadcasting.

### `Program.cs`
Builds the host: `AddApplication` + `AddInfrastructure`, configures JWT bearer + Google
options, SignalR, CORS, controllers with the `ValidationFilter`, the pipeline
(exception middleware → CORS → auth → endpoints), and `MapHub<AuctionHub>("/hubs/auction")`.
On startup it best-effort applies migrations + seeds (logs a warning if infra is down).

### `Controllers/`
| File | Endpoints | Use case |
|---|---|---|
| `AuthController.cs` | `/api/auth/register`, `/login`, `/google`, `/refresh` | UC-01/01A/01B |
| `UsersController.cs` | `GET /api/users/me`, `PUT /api/users/me/phone` | UC-02 |
| `AuctionsController.cs` | `POST /api/auctions`, `DELETE /api/auctions/{id}` | UC-03/04 |
| `CatalogController.cs` | `GET /api/catalog`, `GET /api/auctions/{id}` | UC-05/06 |
| `OrdersController.cs` | `GET /api/orders`, `PATCH /api/orders/{id}/status` | UC-12/13 |

### `Auth/`
| File | Purpose |
|---|---|
| `JwtOptions.cs` | Strongly-typed `Jwt` config section. |
| `JwtTokenService.cs` | Implements `IAuthTokenService`: signs access + refresh JWTs, validates refresh tokens (checks `token_type=refresh`). |
| `GoogleOptions.cs` | `Google` config section. |
| `GoogleTokenValidator.cs` | Validates a Google `id_token` via `GoogleJsonWebSignature` (pins audience to ClientId) → produces `GoogleIdentity`. |
| `BcryptPasswordHasher.cs` | Implements `IPasswordHasher` with BCrypt.Net-Next. |
| `CurrentUserService.cs` | Reads the authenticated `UserId`/`Username` from HttpContext claims. |

### `Hubs/`
| File | Purpose |
|---|---|
| `Contracts/IHubContract.cs` | Canonical SignalR client-method name constants (`ReceiveNewBid`, `AuctionExtended`, `RoomSnapshot`, etc.) — keeps Hub and notifier in sync. |
| `AuctionHub.cs` | `JoinAuctionRoom` (join group + push snapshot), `SendBid` (call `AuctionService.PlaceBidAsync` → broadcast `NewBid`/`AuctionExtended` or `BidRejected`), `OnDisconnectedAsync`. |

### `Middleware/`
| File | Purpose |
|---|---|
| `ExceptionHandlingMiddleware.cs` | Catches unhandled exceptions → logs → 500 JSON envelope. |
| `ValidationFilter.cs` | Converts model-state errors into 422 responses. |

### Top-level API files
| File | Purpose |
|---|---|
| `ApiResponse.cs` | Generic + non-generic JSON response envelope (`{ success, data, errorCode, message }`). |
| `ApiResults.cs` | Maps a `ServiceResult` → correct HTTP status (200/401/403/404/409/422) and envelope. |
| `SignalRAuctionNotifier.cs` | Implements `IAuctionNotifier` using `IHubContext<AuctionHub>` so background jobs can broadcast. |

---

## 7. Tests

| Project | Purpose |
|---|---|
| `ToysAuctionHouse.UnitTests` | Pure unit tests (no infra): state machine transitions, session `PlaceBid`/`Finalize`/`Activate`, `Price` validation, and a static Lua **fidelity** check that the script agrees with the domain rules. |
| `ToysAuctionHouse.IntegrationTests` | Scaffolded for Phase 10 (Redis + SQL + k6 load). |

---

## 8. Request lifecycle (the bid hot path)

1. Client calls SignalR `SendBid(auctionId, amount, idempotencyKey)`.
2. `AuctionHub.SendBid` → `AuctionService.PlaceBidAsync`.
3. Service enforces the **phone gate**, then calls `IRedisBidRepository.PlaceBidAsync`.
4. The repo runs the **single Lua script** atomically:
   - `SET idempotency:{key} PROCESSING EX 30 NX` → duplicate?
   - status must be `Active`, `now` within `[start, end)`
   - `amount >= currentPrice + stepPrice`
   - accept: update meta, `ZADD` to bid ZSET, run anti-sniping extension if within 60s of end
5. If accepted, the repo `XADD`s the bid event to the stream.
6. The Hub broadcasts `ReceiveNewBid` (and `AuctionExtended` if extended).
7. `BidPersistorService` later reads the stream, inserts the bid row (idempotent), and `XACK`s.
8. When the auction expires, `AuctionFinalizerService` marks it Ended, persists session + order in one SQL transaction, and broadcasts `AuctionEnded`.

---

## 9. Config & infra files

| File | Purpose |
|---|---|
| `src/ToysAuctionHouse.Api/appsettings.json` | Prod-safe placeholders (empty secrets). |
| `src/ToysAuctionHouse.Api/appsettings.Development.json` | Local dev connection strings + keys. |
| `docker-compose.yml` | SQL Server 2022 + Redis 7 (AOF everysec) for local dev. |
| `.config/dotnet-tools.json` | Local `dotnet-ef` tool manifest. |
| `.gitignore` | .NET / Node build + secret exclusions. |
| `k6/bid-flow.js` | Load-test stub for Phase 10. |

---

## 10. Design decisions worth knowing

- **Single Lua script = no locks on SQL.** Bids are atomic in Redis; SQL is only a ledger.
- **Redis is the live source of truth**; SQL is durable history. Consistency is eventual via the Stream.
- **Idempotency at both layers**: Redis `SET NX EX 30` (fast reject) + SQL unique `IdempotencyKey` (hard guarantee).
- **Anti-sniping evaluated inside Lua** (it is part of the atomic bid), and mirrored in `AuctionStateMachine.ApplyAntiSniping` for testability.
- **Repo ports live in Application**, not Infrastructure — services depend on abstractions and never touch Redis/EF directly (clean boundaries).
- **Single bid-row writer** (`BidPersistorService`) avoids duplicate-insert races; the finalizer only persists session + order.
