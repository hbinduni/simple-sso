# Simple SSO — Microsoft Entra ID

A lightweight **single sign-on** service: an **ASP.NET Core** backend that brokers Microsoft
Entra ID (Azure AD) logins and issues **stateless JWTs**, with a **React** frontend. No database —
identity and group memberships come straight from the Entra token.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10-512BD4)](https://learn.microsoft.com/aspnet/core)
[![React](https://img.shields.io/badge/React-19-blue)](https://react.dev)
[![Vite](https://img.shields.io/badge/Vite-7-purple)](https://vite.dev)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-4-38bdf8)](https://tailwindcss.com)

## 🎯 What is This?

A minimal, **stateless SSO broker**. The user signs in with Microsoft, the server validates the
Entra authorization code, then mints its own short-lived access token and a refresh token. There is
no user store, no sessions table, no Postgres — the JWT *is* the record of truth.

## ✨ Features

- ✅ **Microsoft Entra ID login** — OAuth 2.0 authorization-code flow with PKCE (confidential client)
- ✅ **Stateless JWTs** — access + refresh tokens; nothing stored server-side
- ✅ **Group memberships** — read from Microsoft Graph at login and surfaced in the profile
- ✅ **No database** — zero persistence, trivial to deploy and scale horizontally
- ✅ **Modern stack** — ASP.NET Core 10 minimal APIs + React 19 + Vite 7 + Tailwind CSS 4
- ✅ **Fail-closed config** — server refuses to start with a missing/weak JWT secret in production
- ✅ **Docker & Kubernetes** — multi-stage images and ready-to-edit manifests

## 📦 Project Structure

```
simple-sso/
├── server/                 # ASP.NET Core backend (stateless SSO broker)
│   ├── Program.cs         # Entry point, DI, middleware, endpoint mapping
│   ├── Auth/              # JwtService, AuthUser, MicrosoftOAuthService
│   ├── Common/            # Validation helpers
│   ├── Configuration/     # AppConfig (env loading)
│   ├── Endpoints/         # Minimal API endpoint groups (root, auth, oauth)
│   ├── Models/            # Request/response records
│   ├── Server.csproj
│   └── Dockerfile
├── client/                 # React + Vite frontend
│   ├── src/
│   │   ├── api/auth.ts    # Token storage + auth calls
│   │   ├── components/auth/ # Login button + profile/groups/logout
│   │   ├── pages/         # Home
│   │   └── ...
│   ├── nginx.conf
│   └── Dockerfile
├── k8s/                    # Kubernetes manifests
├── docker-compose.yml
├── Makefile
└── package.json            # Bun workspace root
```

## 🚀 Quick Start

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) >= 10.0
- [Bun](https://bun.sh) >= 1.3.0 (for the client)
- A Microsoft Entra ID app registration (see below)

### Microsoft Entra ID setup

1. **Entra admin center → App registrations → New registration.**
2. **Authentication → Add a platform → Web** → redirect URI
   `http://localhost:3000/api/auth/oauth/microsoft/callback` (add your production URL too).
3. **Certificates & secrets → New client secret** → copy the *Value*.
4. **API permissions → Microsoft Graph → Delegated →** add `GroupMember.Read.All`, then
   **Grant admin consent** (needed to show group display names; login still works without it).

### Run it

```bash
bun install

# server/.env (git-ignored) — fill in your Entra values
cp server/.env.example server/.env

# Run both server and client
make dev
# server → http://localhost:3000   client → http://localhost:5173
```

Open http://localhost:5173 and click **Sign in with Microsoft**.

## 🔑 API Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/` | GET | No | API info |
| `/health` | GET | No | Health check (uptime + memory) |
| `/api/auth/oauth/microsoft` | GET | No | Start the Microsoft login (redirects to Entra) |
| `/api/auth/oauth/microsoft/callback` | GET | No | OAuth callback → redirects to the SPA with tokens |
| `/api/auth/me` | GET | Yes | Current user + groups (from the JWT) |
| `/api/auth/refresh` | POST | No | Exchange a refresh token for a new access token |
| `/api/auth/logout` | POST | Yes | Client-side logout (stateless) |

After a successful login the server redirects to
`${FRONTEND_URL}/auth/callback#accessToken=…&refreshToken=…&expiresIn=…&tokenType=Bearer`; the SPA
reads the fragment, stores the tokens, and strips them from the URL.

## 🛠️ Tech Stack

**Backend** — .NET 10, ASP.NET Core minimal APIs, `Microsoft.AspNetCore.Authentication.JwtBearer`,
`Microsoft.IdentityModel.JsonWebTokens` (token issuing/parsing), `DotNetEnv`.

**Frontend** — React 19, Vite 7, Tailwind CSS 4, tsgo (TypeScript 7 native type-checker).

**Tooling** — Bun, Biome (lint/format), Docker (multi-stage), Nginx, Kubernetes.

## ⚙️ Configuration

All server config comes from environment variables (`server/.env` locally). See
[ENV_VARS.md](./ENV_VARS.md). Key ones:

| Variable | Required | Notes |
|----------|----------|-------|
| `JWT_SECRET` | ✅ | Signs our tokens; ≥32 chars, fail-closed in production |
| `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` / `AZURE_REDIRECT_URI` | for SSO | Entra app registration; all four enable the Microsoft endpoints |
| `FRONTEND_URL` | ✅ | SPA origin (CORS + post-login redirect) |
| `ENVIRONMENT` | — | `development` relaxes the JWT-secret guard |

> Single-tenant: the callback rejects id_tokens whose `tid` ≠ `AZURE_TENANT_ID`.

## 🐳 Deployment

- **Docker:** `docker compose --env-file .env.production up -d` (see [DOCKER.md](./DOCKER.md)).
- **Kubernetes:** manifests in `k8s/` (see [KUBERNETES.md](./KUBERNETES.md)). Provide `JWT_SECRET`
  and the `AZURE_*` values via the `monorepo-secret` (`make k8s-generate-secret`).

Because the server is stateless, scale it to as many replicas as you like — no shared datastore.

## 📚 Documentation

- [ENV_VARS.md](./ENV_VARS.md) — Environment variables reference
- [DOCKER.md](./DOCKER.md) — Docker deployment guide
- [KUBERNETES.md](./KUBERNETES.md) — Kubernetes deployment guide

## 📄 License

MIT License — free to use for any purpose.
