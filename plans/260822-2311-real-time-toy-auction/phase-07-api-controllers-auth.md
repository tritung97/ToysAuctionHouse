# Phase 07 — API Controllers, JWT & OAuth

> **Context:** [plan.md](./plan.md) · Dependencies: Phase 06 (Services)

## Overview
- **Date:** 2026-08-22 | **Priority:** P1 | **Status:** Pending
- Expose REST controllers, JWT auth (local + Google OAuth via `GoogleJsonWebSignature`), authorization, binding/validation, error mapping, DI wiring.

## Key Insights
- Auth returns an app JWT (AccessToken + RefreshToken) with claims `sub`/`UserId`, `Username`, `Role`.
- OAuth flow (UC-01B): frontend gets Google `id_token` → `POST /api/auth/google` → backend validates with Google public keys → upsert/link account → return app JWT.
- Map service typed results → correct HTTP codes (409, 401, 400, 422).

## Endpoints
- `POST /api/auth/register` (UC-01A), `POST /api/auth/login`, `POST /api/auth/google` (UC-01B), `POST /api/auth/refresh`
- `GET /api/users/me` (UC-02), `PUT /api/users/me/phone`
- `POST /api/products` + `POST /api/auctions` (UC-03), `DELETE /api/auctions/{id}` (UC-04)
- `GET /api/catalog?category=&condition=&status=&page=` (UC-05), `GET /api/auctions/{id}` (UC-06)
- `GET /api/orders` (UC-12), `PATCH /api/orders/{id}/status` (UC-13)

## Files to Create / Modify
- `src/ToysAuctionHouse.Api/Controllers/{AuthController,UsersController,ProductsController,AuctionsController,CatalogController,OrdersController}.cs`
- `src/ToysAuctionHouse.Api/Auth/{JwtTokenService,GoogleTokenValidator,CurrentUserService}.cs`
- `src/ToysAuctionHouse.Api/Middleware/{ExceptionHandlingMiddleware,ValidationFilter}.cs`
- `src/ToysAuctionHouse.Api/Program.cs` — DI + pipeline

## Implementation Steps
1. JWT: configure `AddAuthentication().AddJwtBearer(...)`, validate issuer/audience/signing key, map `sub`→`UserId`.
2. Google OAuth: `GoogleJsonWebSignature.ValidateAsync(idToken, settings)`; on success call `AuthService.LoginWithGoogleAsync`.
3. Password hashing: BCrypt.Net-Next `HashPassword`/`Verify`.
4. Middleware to map `BidResult`/validation problems → 200/400/401/409/422 consistently.
5. DI: register all services + repos + Redis conn + DbContext with correct lifetimes (Redis multiplexer = Singleton; DbContext = Scoped; Hub = Singleton; services = Scoped).
6. CORS allow React dev origin; JWT bearer for SignalR (`access_token` in query string for WebSockets).

## Todo List
- [ ] Local + OAuth login return JWT
- [ ] Errors map to correct HTTP codes
- [ ] [Authorize] enforced on bid/sell/order endpoints

## Success Criteria
- Register/login/google flows return access + refresh token.
- Unauthorized access to protected endpoints returns 401; duplicate email/username returns 409; invalid input returns 422.

## Risk Assessment
- Refresh-token rotation flow needs secure storage — keep simple (opaque refresh token in a `UserRefreshTokens` table or signed token). YAGNI: signed refresh token OK.
- Google token validation must pin expected audience (client id) to prevent token misuse.

## Security Considerations
- OWASP: never log passwords/tokens; validate all inputs; HTTPS in prod; JWT expiry short.
- Refresh tokens revocable; `PasswordHash` never serialized in responses.
