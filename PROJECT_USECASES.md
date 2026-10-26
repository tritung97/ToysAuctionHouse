# DETAILED USE CASE SPECIFICATIONS: Real-Time Toy Auction Platform

## 1. Actor Definitions
- **Guest / Unauthenticated User:** Can browse catalog, search items, and view live rooms in read-only mode.
- **Bidder (Authenticated User):** Can join rooms, place live bids, track outbid status, and view won orders.
- **Seller (Authenticated User):** Can list toys, configure auction parameters, cancel pending sessions, and mark orders fulfilled.
- **System (Background Worker):** Automated tasks for state transitions, countdown enforcement, anti-sniping extensions, and data persistence.

---

## 2. Module 1: Authentication & User Profile (OAuth & Local JWT)

### UC-01: User Registration & Authentication
- **Actors:** Guest, Bidder, Seller
- **Preconditions:** None.
- **Main Flow:**
  1. User submits `Username`, `Email`, `Password`, `FullName`, and `PhoneNumber`.
  2. System validates uniqueness of `Username` and `Email`.
  3. System hashes password (BCrypt/Argon2) and persists record in SQL Server `Users`.
  4. User logs in with credentials; system returns a signed JWT containing `UserId`, `Username`, and `Role`.
- **Exception Flow:**
  - Duplicate email/username -> Return HTTP 409 Conflict.
  - Invalid credentials -> Return HTTP 401 Unauthorized.

### UC-01A: Local Registration & Password Authentication
- **Actors:** Guest
- **Main Flow:**
  1. User submits `Username`, `Email`, `Password`, `FullName`, and `PhoneNumber`.
  2. System verifies uniqueness of `Username` and `Email`.
  3. System hashes password with BCrypt/Argon2, sets `AuthProvider = "Local"`, and persists record.
  4. System generates and returns a signed application JWT (AccessToken + RefreshToken).

### UC-01B: OAuth 2.0 / Social Authentication (Google / GitHub)
- **Actors:** Guest
- **Preconditions:** Client is integrated with Google OAuth2 SDK / Sign-In with Google.
- **Main Flow:**
  1. **Frontend Authentication:** User clicks "Sign in with Google" on ReactJS -> React receives `id_token` (JWT credential) from Google.
  2. **Token Verification:** ReactJS sends `id_token` to Backend API: `POST /api/auth/google`.
  3. Backend validates the token signature against Google's public keys using `GoogleJsonWebSignature.ValidateAsync(id_token)`.
  4. **Account Resolution (Auto-Link / Upsert):**
     - Case 1 (Existing OAuth User): System finds user by `AuthProvider == "Google"` AND `ProviderKey == googlePayload.Subject` -> Generates App JWT.
     - Case 2 (Existing Local Email): If email exists with `AuthProvider == "Local"`, system links the account by updating `ProviderKey` and `AvatarUrl` (if null) -> Generates App JWT.
     - Case 3 (New User): System creates a new record in `Users`:
       - `Email = googlePayload.Email`
       - `Username = googlePayload.Email.Split('@')[0] + "_" + randomDigits`
       - `FullName = googlePayload.Name`
       - `AvatarUrl = googlePayload.Picture`
       - `AuthProvider = "Google"`
       - `ProviderKey = googlePayload.Subject`
       - `PasswordHash = NULL`, `PhoneNumber = NULL`.
  5. System returns application JWT to ReactJS client.

### UC-01C: Enforce Profile Completion (Phone Number Constraint)
- **Actors:** Authenticated User (OAuth registered)
- **Business Rule:** Users who registered via OAuth will have `PhoneNumber == NULL`.
- **Preconditions for Bidding & Selling:**
  - Before executing `UC-03 (Create Auction)` or `UC-08 (Place Live Bid)`, backend checks if `User.PhoneNumber` is present.
  - If null, API/SignalR rejects with error: `PROFILE_PHONE_REQUIRED`, prompting user to enter phone number before participating.

### UC-02: Get User Profile & Contact Info
- **Actors:** Authenticated User
- **Main Flow:** System queries `Users` table by JWT claims and returns profile data (including `PhoneNumber` used for order settlement).

---

## 3. Module 2: Toy Listing & Auction Management (Seller)

### UC-03: Create Toy Product & Schedule Auction Session
- **Actors:** Seller
- **Preconditions:** Seller is authenticated.
- **Input Data:**
  - `Product`: Name, Category, Scale, Brand, Condition (`1: NewSeal`, `2: SecondHand`, `3: Custom`), Description, ImageUrls.
  - `Auction`: StartPrice, StepPrice, StartTime, EndTime, IsAntiSnipingEnabled.
- **Main Flow:**
  1. Seller submits product info and auction schedule via REST API.
  2. System validates constraints: `StartTime >= UtcNow`, `EndTime > StartTime`, `StepPrice > 0`, `StartPrice > 0`.
  3. System saves `ToyProduct` and `AuctionSession` (Status = `1: Scheduled`, `CurrentPrice = StartPrice`) inside a single database transaction.
  4. Returns `AuctionId` and initial metadata.
- **Exception Flow:**
  - `StartTime` in the past -> Return HTTP 400 Bad Request.
  - Missing mandatory fields -> Return HTTP 422 Unprocessable Entity.

### UC-04: Cancel Scheduled Auction
- **Actors:** Seller
- **Preconditions:** Auction status is `Scheduled` (has not started yet).
- **Main Flow:**
  1. Seller sends cancel request for `AuctionId`.
  2. System verifies `SellerId == CurrentUserId` and `Status == Scheduled`.
  3. System updates `AuctionSessions.Status = 4 (Cancelled)`.
- **Exception Flow:**
  - Auction is already `Active` or `Ended` -> Return HTTP 400 with message: "Cannot cancel an ongoing or completed auction".

---

## 4. Module 3: Discovery & Catalog Browsing (Guest / Bidder)

### UC-05: Browse & Filter Auction Catalog
- **Actors:** Guest, Bidder
- **Input Filters:** `Category`, `Condition` (1/2/3), `Status` (Scheduled/Active/Ended), `PageNumber`, `PageSize`.
- **Main Flow:**
  1. Client sends query parameters.
  2. System queries SQL Server utilizing index `IX_ToyProducts_Category_Condition` and `IX_AuctionSessions_Status_EndTime` (or Redis cache for active lists).
  3. Returns paginated list of product cards with current highest price and remaining time.

### UC-06: View Auction Room Detail (Initial Load)
- **Actors:** Guest, Bidder
- **Main Flow:**
  1. Client opens `/auctions/{id}`.
  2. System checks Redis Hash `auction:{id}:meta`.
     - *Cache Hit:* Return current price, end time, seller info, and top bids from Redis.
     - *Cache Miss:* Fetch from SQL Server, populate Redis, and return response.
  3. Frontend renders product gallery, description, live countdown timer, and recent bid history.

---

## 5. Module 4: Real-Time Bidding & Room Lifecycle (Core Engine)

### UC-07: Join Auction Room (SignalR WebSocket)
- **Actors:** Bidder, Guest
- **Main Flow:**
  1. Client establishes SignalR connection: `/hubs/auction`.
  2. Client calls Hub method `JoinAuctionRoom(auctionId)`.
  3. Server adds connection to SignalR group `Auction_{auctionId}`.
  4. Server sends current snapshot (Top 10 bids from `auction:{id}:bids`, remaining seconds) to caller.

### UC-08: Place Live Bid (Ultra-Fast Hot Path)
- **Actors:** Bidder
- **Preconditions:** User is authenticated, auction `Status == Active`, room joined.
- **Input Data:** `AuctionId`, `Amount`, `IdempotencyKey` (UUID).
- **Main Flow:**
  1. Client invokes Hub method `SendBid(payload)`.
  2. Backend executes **Redis Lua Script** (Atomic Execution):
     - **Check 1 (Idempotency):** `SET idempotency:{key} "PROCESSING" EX 30 NX`. If key exists -> Reject duplicate.
     - **Check 2 (Session State):** Verify `auction:{id}:meta.Status == Active` and `UtcNow < EndTime`.
     - **Check 3 (Amount Validity):** Verify `Amount >= CurrentPrice + StepPrice`.
     - **Execution:** 
       - Update `auction:{id}:meta.CurrentPrice = Amount`.
       - Update `auction:{id}:meta.CurrentWinnerId = BidderId`.
       - Insert into Sorted Set: `ZADD auction:{id}:bids {Amount} "{BidderId}:{BidId}:{Timestamp}"`.
  3. **Anti-Sniping Evaluation (Within same transaction/flow):**
     - If `IsAntiSnipingEnabled == true` AND `(EndTime - UtcNow) <= 60 seconds`:
       - Extend `EndTime = EndTime + 120 seconds`.
       - Update Redis `auction:{id}:meta.EndTime`.
       - SignalR broadcasts `AuctionExtended(newEndTime)` to group `Auction_{id}`.
  4. **Broadcast Update:** SignalR pushes `ReceiveNewBid(bidderName, amount, timestamp)` to group `Auction_{id}`.
  5. Asynchronously push bid event to Redis Stream/List for background DB persistence.
- **Exception Flow:**
  - Duplicate `IdempotencyKey` -> Return error: "Duplicate bid request".
  - `Amount < CurrentPrice + StepPrice` -> Return error: "Outbid by another user! Please refresh bid amount".
  - Auction expired -> Return error: "Auction session has ended".

### UC-09: Leave Auction Room
- **Actors:** Bidder, Guest
- **Main Flow:**
  1. Client navigates away or disconnects.
  2. SignalR executes `OnDisconnectedAsync`, removing `ConnectionId` from group `Auction_{auctionId}`.

---

## 6. Module 5: Automated Lifecycle & Order Settlement (System)

### UC-10: Activate Scheduled Auctions (Worker Job)
- **Actors:** System (`BackgroundService`)
- **Execution Interval:** Every 5–10 seconds.
- **Main Flow:**
  1. Worker queries SQL Server for `AuctionSessions` where `Status == Scheduled` AND `StartTime <= UtcNow`.
  2. Updates DB `Status = Active`.
  3. Initializes Redis Keys: `auction:{id}:meta` and `auction:{id}:bids`.
  4. Adds `auctionId` to Redis Set `auction:active_list`.
  5. SignalR broadcasts `AuctionStarted` to notification channel.

### UC-11: Finalize Expired Auctions & Create Settlement Order (Worker Job)
- **Actors:** System (`BackgroundService`)
- **Execution Interval:** Every 1–2 seconds.
- **Main Flow:**
  1. Worker checks active auctions in Redis where `UtcNow >= EndTime`.
  2. Atomically sets `auction:{id}:meta.Status = Ended` to block further bids.
  3. Retrieves top bidder (`WinnerId`) and highest amount (`FinalAmount`) from Redis `ZSET`.
  4. **Persist Transaction in SQL Server:**
     - Update `AuctionSessions.Status = 3 (Ended)`, `CurrentPrice = FinalAmount`, `WinnerId = WinnerId`.
     - Bulk insert all unprocessed bids from Redis into SQL Server `Bids` table.
     - If `WinnerId != null`:
       - Create new record in `Orders` (`AuctionSessionId`, `BuyerId`, `SellerId`, `FinalAmount`, `Status = 1 (Pending)`).
  5. SignalR broadcasts `AuctionEnded(winnerName, finalAmount)` to group `Auction_{id}`.
  6. Clean up temporary Redis runtime keys after grace retention period.

---

## 7. Module 6: Order Fulfill & Contact (Buyer & Seller)

### UC-12: View Won / Sold Orders
- **Actors:** Bidder (Buyer), Seller
- **Main Flow:**
  1. Buyer/Seller queries `/api/orders`.
  2. System returns order list joined with counterparty's `FullName` and `PhoneNumber`.

### UC-13: Update Order Settlement Status
- **Actors:** Seller
- **Preconditions:** `Orders.Status == Pending`.
- **Main Flow:**
  1. Seller contacts Buyer via `PhoneNumber` to arrange delivery/payment (COD/Direct).
  2. Upon completion, Seller calls API to mark `Orders.Status = 2 (Completed)` or `3 (Cancelled)` if buyer failed to respond.
  3. System updates SQL Server `Orders` table.