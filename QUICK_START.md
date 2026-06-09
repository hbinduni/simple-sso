# Quick Start

Simple SSO is a stateless Microsoft Entra ID single sign-on service: a .NET server that brokers
OAuth logins and issues JWTs, plus a React client. No database.

## 1. Prerequisites

- .NET SDK ≥ 10.0
- Bun ≥ 1.3.0
- A Microsoft Entra ID app registration (see [README.md](./README.md#microsoft-entra-id-setup))

## 2. Configure

```bash
bun install
cp server/.env.example server/.env      # git-ignored — put your real Entra values here
```

`server/.env` needs:
```bash
JWT_SECRET=dev-secret-key-change-in-production
AZURE_TENANT_ID=...
AZURE_CLIENT_ID=...
AZURE_CLIENT_SECRET=...
AZURE_REDIRECT_URI=http://localhost:3000/api/auth/oauth/microsoft/callback
FRONTEND_URL=http://localhost:5173
```

Register that `AZURE_REDIRECT_URI` as a **Web** redirect URI on the Entra app. For group names,
add the `GroupMember.Read.All` Graph permission and grant admin consent.

## 3. Run

```bash
make dev            # server (:3000) + client (:5173) together
# or individually:
make dev-server
make dev-client
```

Open http://localhost:5173 and click **Sign in with Microsoft**.

## 4. The login flow

```
Home → GET /api/auth/oauth/microsoft → Entra → GET /api/auth/oauth/microsoft/callback
     → redirect to FRONTEND_URL/auth/callback#accessToken=…&refreshToken=…
     → SPA stores tokens, calls GET /api/auth/me → shows profile + groups
```

## 5. Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/auth/oauth/microsoft` | GET | No | Start Microsoft login |
| `/api/auth/oauth/microsoft/callback` | GET | No | OAuth callback → SPA with tokens |
| `/api/auth/me` | GET | Yes | Current user + groups |
| `/api/auth/refresh` | POST | No | New access token from a refresh token |
| `/api/auth/logout` | POST | Yes | Client-side logout |
| `/health` | GET | No | Liveness |

## 6. Common scripts

```bash
bun run build        # build server + client
bun run typecheck    # tsgo type-check
bun run check        # Biome lint + format (+ dotnet format)
bun test             # tests
```

See [README.md](./README.md) for the full picture and [ENV_VARS.md](./ENV_VARS.md) for all variables.
