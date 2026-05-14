# Server Migration: Go/Fiber → C# .NET 10

**Date:** 2026-05-14
**Status:** Approved design

## Goal

Replace the Go/Fiber server in `server/` with an ASP.NET Core (.NET 10 LTS)
implementation, preserving the existing HTTP API contract so the React client
needs zero changes. Update `Makefile`, `server/Dockerfile`, and `k8s/` manifests
to match.

## Decisions

| Topic        | Choice                                                        |
|--------------|---------------------------------------------------------------|
| Runtime      | .NET 10 LTS                                                   |
| API style    | ASP.NET Core Minimal APIs                                     |
| Data access  | Dapper + Npgsql (SQL-first; `db/schema.sql` stays the source of truth) |
| API parity   | Parity + cleanup (compatible contract, small additive improvements) |
| TypeID       | Hand-rolled helper using `Guid.CreateVersion7()` + Crockford base32 |
| Auth         | `JwtBearer` middleware for protection; `JwtService` for issuing + manual refresh validation |

## Dependencies (NuGet)

- `Dapper`
- `Npgsql`
- `BCrypt.Net-Next`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `DotNetEnv` — loads local `.env` (mirrors Go's `godotenv`)
- `Microsoft.AspNetCore.OpenApi` — OpenAPI document (cleanup)

## Project Structure (`server/`)

```
Server.csproj
Program.cs                       -- builder, DI, middleware pipeline, endpoint mapping
Version.cs                       -- public const string Version (Makefile-sed target)
appsettings.json
appsettings.Development.json
.env.example                     -- kept for parity (DotNetEnv loads it in dev)
Dockerfile
Configuration/
  AppConfig.cs                   -- bound config: Environment, Port, DatabaseUrl, FrontendUrl, JwtSecret
Data/
  Database.cs                    -- NpgsqlDataSource wrapper + Health()
  UserRepository.cs              -- CreateUser, GetUserById, GetUserByEmail
  SessionRepository.cs           -- CreateSession, GetSessionById, GetUserSessions, DeleteSession
  ItemRepository.cs              -- CreateItem, GetItemById, GetUserItems, UpdateItem, DeleteItem
  OAuthRepository.cs             -- CreateOAuthAccount, GetOAuthAccount
Models/
  User.cs, Item.cs, Session.cs, OAuthAccount.cs   -- records + string-backed enums
  Requests.cs                    -- RegisterRequest, LoginRequest, RefreshTokenRequest, item create/update
  Responses.cs                   -- AuthResponse, RefreshTokenResponse, ApiResponse<T>
Auth/
  JwtService.cs                  -- GenerateAccessToken, GenerateRefreshToken, ValidateToken
  PasswordService.cs             -- Hash / Verify (BCrypt, cost 12)
Endpoints/
  AuthEndpoints.cs               -- MapAuthEndpoints
  ItemEndpoints.cs               -- MapItemEndpoints
  RootEndpoints.cs               -- "/" and "/health"
Common/
  TypeId.cs                      -- NewUserId/NewItemId/NewSessionId/NewOAuthAccountId, Validate
  Validation.cs                  -- ValidateEmail, NormalizeEmail, ValidatePassword
  ClientIp.cs                    -- X-Forwarded-For / X-Real-IP / RemoteIpAddress
```

## API Contract (must stay compatible)

### Routes
- `GET /` — API info JSON
- `GET /health` — `{status, timestamp, uptime, memory{...}, database}`
  - uptime from process start; memory via `GC.GetTotalMemory` / `Environment.WorkingSet`
  - `database`: `healthy` / `unhealthy` / `not_configured`
- `POST /api/auth/register` — 201, `ApiResponse<AuthResponse>`
- `POST /api/auth/login` — 200, `ApiResponse<AuthResponse>`
- `POST /api/auth/refresh` — 200, `ApiResponse<RefreshTokenResponse>`
- `POST /api/auth/logout` — protected, 200
- `GET /api/auth/me` — protected, 200, `ApiResponse<User>`
- `GET /api/auth/sessions` — protected, 200, `ApiResponse<Session[]>`
- `GET|POST /api/items`, `GET|PUT|DELETE /api/items/:id` — all protected
- 404 fallback: `ApiResponse` error `"Route not found: METHOD path"`

### JSON conventions
- camelCase property names
- `{ success: bool, data?: T, error?: string, message?: string }` envelope
- `password_hash`, OAuth `access_token`/`refresh_token` never serialized
- Nullable fields omitted when null (`JsonIgnoreCondition.WhenWritingNull`)
- Enums serialized as strings (`role`, `status`, `provider`, token `type`)

### Auth behaviour
- JWT HS256, signed with `JWT_SECRET`
- Access token expiry 15 min, refresh token expiry 7 days
- Claims: `sub` (user id), `email`, `role`, `type` (`access` | `refresh`), `iat`, `exp`
- `JwtBearer` middleware validates access tokens on protected endpoints; a policy
  rejects tokens whose `type != access`
- `JwtBearerEvents.OnChallenge` / `OnForbidden` write the `{success,error}` envelope
  so 401/403 bodies match the current server
- Refresh endpoint validates the refresh token manually via `JwtService`
  (checks `type == refresh`)
- Server binds Kestrel to `PORT` env var (default `3000`)

### Cleanup (additive, non-breaking)
- OpenAPI document at `/openapi/v1.json`
- Real per-field validation messages on register / item create (still inside the
  `{success,error}` envelope, still HTTP 400)

## Data Layer

- `Database.cs` builds an `NpgsqlDataSource` from `DATABASE_URL`. Pool tuning to
  match Go: max 25, min 5, idle/lifetime timeouts. `Health()` runs `SELECT 1`
  with a short timeout.
- Repositories use Dapper, one method per existing `queries.go` function, same
  SQL. `RETURNING` clauses map back onto the record (timestamps).
- When `DATABASE_URL` is empty: server still starts, auth/items routes that need
  the DB are not mapped, `GET /api/items` returns empty `data`, `/health` reports
  `database: not_configured` — same as Go.

## Makefile Changes

- `build-server`: `cd server && dotnet publish -c Release -o bin`
- `fmt-server`: `cd server && dotnet format`
- `lint-server`: `cd server && dotnet format --verify-no-changes`
- `check-server`: `cd server && dotnet format`
- `deps-server`: `cd server && dotnet list package --outdated`
- `version-up`: retarget `server/version.go` → `server/Version.cs`; the existing
  `Version = "x.y.z"` sed pattern still applies
- Remove references to Go tooling; delete `server/.golangci.yml`, `server/go.mod`,
  `server/go.sum`
- `typecheck` (client-only) unchanged
- Project/image/namespace names (`bun-hono-react*`) left unchanged

## server/Dockerfile

- Multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` (build) →
  `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` (runtime)
- Build context is the repo root (matches `docker build -f server/Dockerfile ... .`
  in the Makefile); copy `server/*.csproj` then restore, then `server/` source
- Non-root user, `EXPOSE 3000`, `ENV ASPNETCORE_URLS=http://+:3000`
- `HEALTHCHECK` with `wget --spider http://localhost:3000/health`

## k8s Changes

- `server-deployment.yaml`:
  - env `NODE_ENV` → `ENVIRONMENT` (fixes latent mismatch — Go read `ENVIRONMENT`
    but the manifest supplied `NODE_ENV`)
  - bump `resources`: requests `memory: 256Mi`, limits `memory: 512Mi`
    (`cpu` unchanged); accounts for the .NET runtime baseline
  - liveness/readiness probes unchanged
- `configmap.yaml`: rename key `NODE_ENV` → `ENVIRONMENT`
- `docker-compose.yml`: already passes `ENVIRONMENT`; only the `# Go/Fiber Server`
  comment changes

## Root package.json Changes

- `dev:server`: `cd server && dotnet watch run`
- `build:server`: `cd server && dotnet publish -c Release -o bin`
- `db:*` scripts unchanged (still read `server/.env`)

## Out of Scope

- Renaming the project / Docker images / k8s namespace
- Rewriting README and other `.md` docs (will be flagged, not edited)
- `tests/health-check.test.ts` is language-agnostic — kept as-is and used to
  verify parity

## Verification

1. `cd server && dotnet build` — compiles clean
2. `dotnet format --verify-no-changes` — formatting clean
3. Server starts with and without `DATABASE_URL`
4. `tests/health-check.test.ts` passes against the running .NET server
5. Manual contract check: register → login → refresh → me → items CRUD return
   the same shapes/status codes as the Go server
6. `docker build -f server/Dockerfile -t test .` succeeds; container healthcheck
   goes healthy
