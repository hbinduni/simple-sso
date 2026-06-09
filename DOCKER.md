# Docker Deployment Guide

Deploying the Simple SSO service with Docker. Two stateless services — a .NET server and an
Nginx-served React client. There is no database.

## 📋 Prerequisites

- Docker Engine 20.10+ and Docker Compose v2
- A Microsoft Entra ID app registration (see [README.md](./README.md#microsoft-entra-id-setup))
- Make (optional, for the build/push convenience targets)

## 🏗️ Architecture

```
┌─────────────────┐        ┌─────────────────┐
│  React Client   │  :80   │   .NET Server   │  :3000
│  (Nginx)        │ ─────▶ │  (SSO broker)   │ ─────▶  Microsoft Entra ID
└─────────────────┘        └─────────────────┘
```

The server holds no state — it brokers Entra logins and issues JWTs, so it scales horizontally
with no shared backing store.

## 🚀 Quick Start

### 1. Configure environment

```bash
cp .env.production.example .env.production
nano .env.production
```

Set at least:
- `JWT_SECRET` — strong value, ≥32 chars (`openssl rand -base64 32`)
- `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_REDIRECT_URI`
- `VITE_API_URL` — the client's view of the server (e.g. `https://api.your-domain.com`)
- `FRONTEND_URL` — the client origin (CORS + post-login redirect)
- `GITHUB_USER` — for pulling images from GHCR

### 2. Build & push images (GHCR)

```bash
export GITHUB_USER=your-github-username
export GITHUB_TOKEN=ghp_your_token

make deploy        # login + build server & client + push (or run the steps individually)
# make docker-build-all
# make push-all
```

### 3. Start

```bash
docker compose --env-file .env.production pull
docker compose --env-file .env.production up -d
docker compose --env-file .env.production ps
```

- **Client**: http://localhost (port 80)
- **Server API**: http://localhost:3000

## 🔧 Common Commands

```bash
# Logs
docker compose --env-file .env.production logs -f
docker compose --env-file .env.production logs -f server

# Restart / stop
docker compose --env-file .env.production up -d server   # recreate one service
docker compose --env-file .env.production down            # stop all

# Shell into the server container
docker compose --env-file .env.production exec server sh
```

## 📝 Environment Variables (.env.production)

| Variable | Required | Description |
|----------|----------|-------------|
| `JWT_SECRET` | ✅ | Signs the access/refresh tokens; ≥32 chars |
| `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` / `AZURE_REDIRECT_URI` | ✅ (for SSO) | Entra app registration |
| `FRONTEND_URL` | ✅ | Client origin (CORS + post-login redirect) |
| `VITE_API_URL` | ✅ | API URL the client calls (runtime-injected) |
| `ENVIRONMENT` | — | `production` (default) or `development` |
| `SERVER_PORT` / `CLIENT_PORT` | — | Host ports (default 3000 / 80) |
| `GITHUB_USER` / `IMAGE_REGISTRY` / `PROJECT_NAME` / `IMAGE_VERSION` | — | Image path components |

See [ENV_VARS.md](./ENV_VARS.md) for the full reference.

## 🔐 Security

1. **Never commit** `.env.production` with real values (it's git-ignored).
2. Generate a strong `JWT_SECRET`; the server refuses to start with a missing/weak one in production.
3. Keep `AZURE_CLIENT_SECRET` in your secret store; rotate it if exposed.
4. Terminate TLS at a reverse proxy (Nginx/Traefik/Caddy) in front of the services.
5. `AZURE_REDIRECT_URI` must use your real public HTTPS URL and be registered in Entra.

## 🐛 Troubleshooting

**Server won't start** — check `docker compose --env-file .env.production logs server`. The most
common cause is a missing/weak `JWT_SECRET` in production (the server fails closed by design).

**Login returns to the SPA with `#error=...`** — verify `AZURE_REDIRECT_URI` matches the redirect
URI registered on the Entra app exactly, and that the four `AZURE_*` values are set in the container
(`docker compose --env-file .env.production exec server env | grep AZURE`).

**Empty Groups in the profile** — add the `GroupMember.Read.All` Graph permission and grant admin
consent on the Entra app registration.

## 📚 Resources

- [Docker Compose Reference](https://docs.docker.com/compose/compose-file/)
- [KUBERNETES.md](./KUBERNETES.md) — Kubernetes deployment
- [ENV_VARS.md](./ENV_VARS.md) — Environment variables
