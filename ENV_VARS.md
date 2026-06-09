# Environment Variables Reference

This document explains which environment variables are needed where and when.

## Variable Categories

### 🔨 Build-Time Variables (Local Machine Only)

Set these **before building Docker images** (in `.env.build` or export manually):

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `GITHUB_USER` | ✅ Yes | GitHub username for GHCR | `hbinduni` |
| `GITHUB_TOKEN` | ✅ Yes | GitHub PAT for docker login | `ghp_xxxxx` |
| `IMAGE_VERSION` | ⚠️ Optional | Image tag (defaults to `latest`) | `v1.0.0` |
| `PROJECT_NAME` | ⚠️ Optional | Project name (defaults to repo name) | `bun-hono-react-monorepo` |

**✨ Note:** `VITE_API_URL` is now a **runtime variable**! No need to set it at build time.

**Usage:**
```bash
# Option 1: Use .env.build file
cp .env.build.example .env.build
# Edit .env.build
source .env.build
make deploy

# Option 2: Export manually
export GITHUB_USER=hbinduni
export GITHUB_TOKEN=ghp_xxxxx
export VITE_API_URL=https://server.larahq.com
make deploy
```

### 🚀 Runtime Variables (VPS `.env.production`)

Set these in `.env.production` **on your VPS** for docker-compose:

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `DATABASE_URL` | ✅ Yes | PostgreSQL connection string | `postgresql://user:pass@localhost:5432/app_db` |
| `GITHUB_USER` | ✅ Yes | GitHub username (for pulling images) | `hbinduni` |
| `VITE_API_URL` | ✅ Yes | **Client API endpoint (runtime!)** | `https://server.larahq.com` |
| `NODE_ENV` | ⚠️ Optional | Node environment (defaults to `production`) | `production` |
| `SERVER_PORT` | ⚠️ Optional | Server port (defaults to `3000`) | `3000` |
| `CLIENT_PORT` | ⚠️ Optional | Client port (defaults to `80`) | `80` |
| `IMAGE_REGISTRY` | ⚠️ Optional | Registry URL (defaults to `ghcr.io`) | `ghcr.io` |
| `PROJECT_NAME` | ⚠️ Optional | Project name (defaults to repo name) | `bun-hono-react-monorepo` |
| `IMAGE_VERSION` | ⚠️ Optional | Image version to pull (defaults to `latest`) | `latest` or `v1.0.0` |
| `SERVER_CONTAINER_NAME` | ⚠️ Optional | Server container name | `monorepo-server` |
| `CLIENT_CONTAINER_NAME` | ⚠️ Optional | Client container name | `monorepo-client` |
| `COMPOSE_PROJECT_NAME` | ⚠️ Optional | Docker Compose project name | `monorepo` |

**Why `GITHUB_USER` is needed:**
Docker Compose constructs the image path as:
```
${IMAGE_REGISTRY}/${GITHUB_USER}/${PROJECT_NAME}-server:${IMAGE_VERSION}
ghcr.io/hbinduni/bun-hono-react-monorepo-server:latest
```

### 💻 Development Variables (Local `.env` files)

#### `server/.env`
```bash
DATABASE_URL=postgresql://user:password@localhost:5432/app_db
PORT=3000
NODE_ENV=development
```

#### `client/.env`
```bash
VITE_API_URL=http://localhost:3000
```

### 🔐 Microsoft Entra ID (Azure AD) OAuth — server only, optional

Set these in `server/.env` (local) or the runtime secret (Docker/K8s). When **all four**
are present the server mounts `GET /api/auth/oauth/microsoft` and its `/callback`; leave any
blank to disable "Sign in with Microsoft" entirely.

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `AZURE_TENANT_ID` | ✅ (to enable) | Directory (tenant) ID of the app registration | `4b859b46-...` |
| `AZURE_CLIENT_ID` | ✅ (to enable) | Application (client) ID | `3d94698a-...` |
| `AZURE_CLIENT_SECRET` | ✅ (to enable) | Client secret value (**not** the secret ID) | `Hrt8Q~...` |
| `AZURE_REDIRECT_URI` | ✅ (to enable) | Callback URL — must match the Entra app exactly | `http://localhost:3000/api/auth/oauth/microsoft/callback` |

**Entra app registration setup:**
1. Entra admin center → App registrations → your app.
2. **Authentication** → Add a platform → **Web** → add the redirect URI above (use your real
   API origin in production, e.g. `https://api.your-domain.com/api/auth/oauth/microsoft/callback`).
3. **Certificates & secrets** → New client secret → copy the **Value** into `AZURE_CLIENT_SECRET`.
4. **API permissions** → Microsoft Graph → Delegated → add `GroupMember.Read.All`, then
   **Grant admin consent** (needs a tenant admin). This lets the sign-in read the user's group
   display names for the profile. Without it, login still works but the Groups list is empty.

**Login flow:** the browser hits `GET /api/auth/oauth/microsoft` → Entra → `/callback`, then the
server issues its own JWTs and redirects to `${FRONTEND_URL}/auth/callback#accessToken=...&refreshToken=...&expiresIn=...&tokenType=Bearer`.
The SPA reads the URL fragment, stores the tokens, and strips them from history.

> ⚠️ The client secret is a credential. Keep it only in `server/.env` (git-ignored) or your
> K8s/Docker secret store, and rotate it if it is ever shared or exposed.

## Common Mistakes

### ✅ NEW: VITE_API_URL is now runtime configurable!
```bash
# .env.production on VPS
VITE_API_URL=https://server.larahq.com  # ✅ Works! No rebuild needed!

# Just restart the container to apply changes
docker compose --env-file .env.production up -d client
```

**Why:** We now use runtime configuration injection. The client reads `VITE_API_URL` from a config file generated at container startup.

### ❌ Wrong: Omitting GITHUB_USER from .env.production
```bash
# .env.production without GITHUB_USER
# Docker Compose can't construct image path!
```

### ✅ Correct: Include GITHUB_USER in .env.production
```bash
# .env.production
GITHUB_USER=hbinduni
PROJECT_NAME=bun-hono-react-monorepo
IMAGE_VERSION=latest
```

## File Summary

| File | Location | Purpose | Contains |
|------|----------|---------|----------|
| `.env.build` | Local | Build-time config | `GITHUB_USER`, `GITHUB_TOKEN`, `VITE_API_URL` |
| `.env.production` | VPS | Runtime config for docker-compose | `DATABASE_URL`, `GITHUB_USER`, ports, container names |
| `server/.env` | Local | Server development | `DATABASE_URL`, `PORT`, `NODE_ENV` |
| `client/.env` | Local | Client development | `VITE_API_URL` |

## Quick Reference

**Building images locally:**
```bash
source .env.build  # Contains GITHUB_USER, GITHUB_TOKEN, VITE_API_URL
make deploy
```

**Deploying on VPS:**
```bash
# .env.production contains GITHUB_USER, DATABASE_URL, etc.
docker compose --env-file .env.production pull
docker compose --env-file .env.production up -d
```

**Updating client API URL:**
```bash
# On local machine (not VPS!)
export VITE_API_URL=https://new-api-domain.com
make build-client push-client

# On VPS
docker compose --env-file .env.production pull client
docker compose --env-file .env.production up -d client
```
