# SYSTEM SPECIFICATION: Real-Time Collectible Toy Auction Platform

## 1. Context & Objectives
You are an expert full-stack software architect assisting in developing a real-time online auction platform tailored for collectible toys and scale models (Gunpla, S.H.Figuarts, Hot Toys, Custom Painted/Kitbash kits).

The system handles high-concurrency, time-critical bidding wars without locking the primary relational database.

---

## 2. Tech Stack & Roles
- **Backend:** .NET 8 / ASP.NET Core Web API (Clean Architecture / Modular Monolith)
- **Real-Time Communication:** SignalR Hub (WebSocket transport with auto-reconnect)
- **Fast In-Memory Layer:** Redis (Sorted Sets `ZSET`, Hashes, Lua Scripts, Atomic Locks)
- **Primary Database:** SQL Server with Entity Framework Core (Persistence & History)
- **Frontend:** React.js (TypeScript, Custom Hooks for SignalR lifecycle, TailwindCSS)
- **Testing / Load Test:** k6 (Stress testing WebSocket connections and concurrent bids)

---

## 3. High-Concurrency Architecture & Data Flow
1. **Write Path (Ultra-Fast Hot Path):**
   - Client sends bid through SignalR Hub: `SendBid(auctionId, amount, idempotencyKey)`.
   - Backend evaluates incoming bid atomically using **Redis Lua Scripts**:
     - Check `IdempotencyKey` existence (`SETNX` with short TTL).
     - Verify `amount >= currentPrice + stepPrice` and `status == Active`.
     - Update metadata and push bid into Redis Sorted Set: `ZADD auction:{id}:bids {amount} "{bidderId}:{timestamp}"`.
   - SignalR immediately broadcasts the new top bid to all clients in group `Auction_{id}`.
2. **Read Path:**
   - Active room state and top bids are read directly from Redis (`ZREVRANGE`, `HGETALL`).
   - Static toy catalogue, seller info, and historical data are fetched from SQL Server with Cache-Aside.
3. **Persistence Path (Asynchronous Consistency):**
   - An ASP.NET Core `BackgroundService` runs continuously to manage session transitions:
     - Promotes `Scheduled` -> `Active` on start time.
     - Detects expired auctions, locks the room, retrieves the winner from Redis, and commits transactions into SQL Server (`Orders` & `AuctionSessions.WinnerId`).

---

## 4. Domain & Business Rules
- **Toy Condition Hierarchy (Fixed Enum):**
  - `1: NewSeal` (Mint in sealed box)
  - `2: SecondHand` (Opened box, used, snap-fit)
  - `3: Custom` (Painted, panel-lined, LED mod, kitbash)
  - *All specific flaws, box conditions, or parts details are stored as unstructured text in `ToyProduct.Description`.*
- **Anti-Sniping (Soft-Close Mechanism):**
  - If a valid bid is placed within the last 60 seconds (`EndTime - UtcNow < 60s`), the system automatically extends `EndTime` by 120 seconds.
  - Broadcasts `AuctionExtended` event to update all client timers.
- **Idempotency Guarantee:**
  - Every bid request requires an `idempotencyKey` (UUID generated on client button press).
  - Enforced via Redis lock and backed by a Unique Index in SQL Server `Bids.IdempotencyKey`.
- **Order Finalization (No Payment Gateway):**
  - When an auction ends, the system creates a pending `Order` record with buyer and seller phone numbers for direct offline contact/COD.

---

## 5. Detailed Relational Database Design (SQL Server)

### 5.1. Entity-Relationship Diagram (Textual Representation)
- `Users` (1) ────< (N) `ToyProducts` (Seller owns Products)
- `ToyProducts` (1) ──── (1) `AuctionSessions` (Each product belongs to exactly one auction)
- `AuctionSessions` (1) ────< (N) `Bids` (An auction accumulates multiple bids)
- `Users` (1) ────< (N) `Bids` (A user places multiple bids)
- `AuctionSessions` (1) ──── (1) `Orders` (An auction creates exactly one settlement order)
- `Users` (1) ────< (N) `Orders` (A user acts as Buyer or Seller in Orders)

---

### 5.2. Detailed Table Specifications

#### Table: `Users`
Stores account and contact information for buyers and sellers.
| Column | Data Type | Nullable | Constraints & Defaults | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INT` | No | PK, `IDENTITY(1,1)` | Unique user identifier |
| `Username` | `NVARCHAR(50)` | No | `UNIQUE` | Unique login username |
| `Email` | `NVARCHAR(150)` | No | `UNIQUE` | Unique login and notification email |
| `PasswordHash` | `NVARCHAR(255)` | No | | Securely hashed password (BCrypt/Argon2) |
| `FullName` | `NVARCHAR(100)` | No | | Display name of the user |
| `PhoneNumber` | `NVARCHAR(20)` | No | | Contact phone number for offline trade/COD |
| `AvatarUrl` | `NVARCHAR(500)` | Yes | | Link to avatar image |
| `CreatedAt` | `DATETIME2` | No | `DEFAULT SYSUTCDATETIME()` | Account creation timestamp (UTC) |

#### Table: `ToyProducts`
Stores toy specifications, categories, and unstructured physical condition details.
| Column | Data Type | Nullable | Constraints & Defaults | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INT` | No | PK, `IDENTITY(1,1)` | Unique product identifier |
| `SellerId` | `INT` | No | FK -> `Users(Id)` | Reference to the seller |
| `Name` | `NVARCHAR(250)` | No | | Name of the figure/kit (e.g., "MG Barbatos") |
| `Category` | `NVARCHAR(50)` | No | | Category (Gunpla, SHF, HotToys, Figma, Resin, Others) |
| `Scale` | `NVARCHAR(20)` | Yes | | Model scale (1/144, 1/100, 1/12, 1/6, Non-scale) |
| `Brand` | `NVARCHAR(100)` | No | | Manufacturer (Bandai, Medicom, Good Smile...) |
| `Condition` | `TINYINT` | No | `CHECK (Condition IN (1, 2, 3))` | `1: NewSeal`, `2: SecondHand`, `3: Custom` |
| `Description` | `NVARCHAR(MAX)` | No | | Free-form details (box state, joint condition, LED/paint) |
| `ImageUrls` | `NVARCHAR(MAX)` | No | | JSON array containing uploaded image URLs |
| `CreatedAt` | `DATETIME2` | No | `DEFAULT SYSUTCDATETIME()` | Creation timestamp (UTC) |

#### Table: `AuctionSessions`
Manages the auction timeline, pricing parameters, and active status.
| Column | Data Type | Nullable | Constraints & Defaults | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INT` | No | PK, `IDENTITY(1,1)` | Unique auction session identifier |
| `ProductId` | `INT` | No | FK -> `ToyProducts(Id)`, `UNIQUE` | 1-to-1 link to product |
| `StartPrice` | `DECIMAL(18,2)` | No | | Opening bid price |
| `StepPrice` | `DECIMAL(18,2)` | No | | Minimum bid increment |
| `CurrentPrice` | `DECIMAL(18,2)` | No | | Highest accepted bid price |
| `StartTime` | `DATETIME2` | No | | Scheduled start timestamp (UTC) |
| `EndTime` | `DATETIME2` | No | | Scheduled/Extended end timestamp (UTC) |
| `Status` | `TINYINT` | No | `DEFAULT 1`, `CHECK (Status IN (1,2,3,4))` | `1: Scheduled`, `2: Active`, `3: Ended`, `4: Cancelled` |
| `IsAntiSnipingEnabled` | `BIT` | No | `DEFAULT 1` | Soft-close rule toggle |
| `WinnerId` | `INT` | Yes | FK -> `Users(Id)` | Final winning bidder ID (set upon completion) |
| `CreatedAt` | `DATETIME2` | No | `DEFAULT SYSUTCDATETIME()` | Creation timestamp (UTC) |

#### Table: `Bids`
Persistent audit log of all validated bids placed across sessions.
| Column | Data Type | Nullable | Constraints & Defaults | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `BIGINT` | No | PK, `IDENTITY(1,1)` | Unique bid identifier |
| `AuctionSessionId` | `INT` | No | FK -> `AuctionSessions(Id)` | Associated auction session |
| `BidderId` | `INT` | No | FK -> `Users(Id)` | User who placed the bid |
| `Amount` | `DECIMAL(18,2)` | No | | Amount offered |
| `IdempotencyKey` | `NVARCHAR(64)` | No | `UNIQUE` | Client UUID preventing duplicate submissions |
| `CreatedAt` | `DATETIME2` | No | `DEFAULT SYSUTCDATETIME()` | Placement timestamp (UTC) |

#### Table: `Orders`
Final settlement generated after an auction session ends successfully.
| Column | Data Type | Nullable | Constraints & Defaults | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INT` | No | PK, `IDENTITY(1,1)` | Unique order identifier |
| `AuctionSessionId` | `INT` | No | FK -> `AuctionSessions(Id)`, `UNIQUE` | 1-to-1 link to ended auction |
| `BuyerId` | `INT` | No | FK -> `Users(Id)` | Winning buyer |
| `SellerId` | `INT` | No | FK -> `Users(Id)` | Product seller |
| `FinalAmount` | `DECIMAL(18,2)` | No | | Winning bid amount |
| `Status` | `TINYINT` | No | `DEFAULT 1`, `CHECK (Status IN (1,2,3))` | `1: Pending (Contact)`, `2: Completed`, `3: Cancelled` |
| `Note` | `NVARCHAR(500)` | Yes | | Delivery notes or transaction remarks |
| `CreatedAt` | `DATETIME2` | No | `DEFAULT SYSUTCDATETIME()` | Order creation timestamp (UTC) |

---

### 5.3. Indexing & Optimization Strategy
```sql
-- 1. Product catalog search and condition filtering
CREATE NONCLUSTERED INDEX IX_ToyProducts_Category_Condition 
ON ToyProducts (Category, Condition);

-- 2. Background worker polling active sessions and expiration checks
CREATE NONCLUSTERED INDEX IX_AuctionSessions_Status_EndTime 
ON AuctionSessions (Status, EndTime) 
INCLUDE (CurrentPrice, WinnerId);

-- 3. Fast retrieval of bid history ordered by highest amount
CREATE NONCLUSTERED INDEX IX_Bids_AuctionSessionId_Amount 
ON Bids (AuctionSessionId, Amount DESC);