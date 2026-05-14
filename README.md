# .NET + React Monorepo Template

A modern full-stack monorepo template with **ASP.NET Core** backend, **React** frontend, **PostgreSQL** database, and **Docker/Kubernetes** deployment.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10-512BD4)](https://learn.microsoft.com/aspnet/core)
[![React](https://img.shields.io/badge/React-19-blue)](https://react.dev)
[![Vite](https://img.shields.io/badge/Vite-7-purple)](https://vite.dev)
[![tsgo](https://img.shields.io/badge/tsgo-7.0--preview-blue)](https://github.com/microsoft/typescript-go)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-4-38bdf8)](https://tailwindcss.com)

## 🎯 What is This?

A **production-ready monorepo template** for building full-stack applications with a .NET backend and React frontend. Clone it, customize it, and start building your project in minutes.

## ✨ Features

- ✅ **Modern Stack**: ASP.NET Core + React 19 + Vite 7 + Tailwind CSS 4
- ✅ **Production Backend**: .NET 10 minimal API with JWT auth, Dapper, and Npgsql
- ✅ **Fast Type-Checking**: tsgo (TypeScript 7 native compiler) — ~10x faster than tsc
- ✅ **Authentication**: JWT tokens with access/refresh pattern, session tracking
- ✅ **Type Safety**: TypeScript strict mode frontend, nullable-enabled C# backend
- ✅ **TypeID**: K-sortable, type-safe identifiers (`user_`, `item_`, `sess_`)
- ✅ **Database**: PostgreSQL 16 with schema, triggers, and seed scripts
- ✅ **Lean Images**: Multi-stage .NET server image and Nginx client image
- ✅ **Docker**: Production-ready multi-stage builds
- ✅ **Kubernetes**: Complete K8s deployment with 40+ Makefile commands
- ✅ **Runtime Config**: Change API URL without rebuilding the client image
- ✅ **Hot Reload**: Development mode with instant updates

## 📦 Project Structure

```
monorepo/
├── server/              # ASP.NET Core backend
│   ├── Program.cs      # Server entry point, DI, middleware, endpoint mapping
│   ├── Auth/           # JWT and password services
│   ├── Common/         # TypeID, validation, and request utilities
│   ├── Configuration/  # App configuration
│   ├── Data/           # PostgreSQL connection and repositories
│   ├── Endpoints/      # Minimal API endpoint groups
│   ├── Models/         # Data models and request/response types
│   ├── Server.csproj   # .NET project file
│   ├── Version.cs      # Server application version
│   ├── .env.example
│   └── Dockerfile      # Multi-stage .NET build
├── client/              # React + Vite frontend
│   ├── src/
│   │   ├── components/ # Reusable UI & layout components
│   │   ├── pages/      # Page components
│   │   ├── api/        # Typed API client layer
│   │   ├── types/      # TypeScript types
│   │   ├── utils/      # Client utilities
│   │   ├── App.tsx     # Root component
│   │   ├── main.tsx    # Client entry point
│   │   └── config.ts   # Runtime configuration
│   ├── nginx.conf
│   ├── Dockerfile
│   └── package.json    # @monorepo/client
├── db/                  # Database schema and seeds
│   ├── schema.sql      # PostgreSQL schema with triggers
│   └── seed.sql        # Sample data
├── k8s/                 # Kubernetes manifests
├── tests/               # Integration tests
├── docker-compose.yml   # Docker orchestration
├── Makefile            # Build & deploy commands
└── package.json        # Bun workspace root
```

### Architecture

- **server/**: ASP.NET Core REST API with JWT auth, repositories, and PostgreSQL access
- **client/**: React 19 + Vite 7 frontend with TypeScript, type-checked by tsgo
- **db/**: PostgreSQL schema with TypeID support, triggers, and session cleanup

## 🚀 Quick Start

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) >= 10.0
- [Bun](https://bun.sh) >= 1.3.0 (for client)
- [Docker](https://docker.com) (for production deployment)
- PostgreSQL 16 (for local development)

### Installation

```bash
# Clone the repository
git clone <your-repo-url>
cd bun-dotnet-react-monorepo

# Install client dependencies
bun install

# Set up environment files
cp server/.env.example server/.env
cp client/.env.example client/.env

# Set up database (PostgreSQL must be running)
bun run db:create  # Create schema
bun run db:seed    # Add sample data
```

### Development

```bash
# Run both server and client concurrently
make dev

# Or run individually:
make dev-server      # .NET server on http://localhost:3000
make dev-client      # React client on http://localhost:5173
                     # API requests are proxied to the server automatically
```

## 🔧 Development Workflows

### Building

```bash
# Build both server and client
bun run build

# Build specific parts
make build-server     # Publishes .NET server to server/bin/
bun run build:client  # Type-checks with tsgo, then builds to client/dist/

# Or use Makefile
make build            # Builds both
make build-server     # .NET server only
make build-client     # React only
```

### Type-Checking

```bash
# Type-check both root tests and client with tsgo (~10x faster than tsc)
bun run typecheck

# Safe fallback using tsc
bun run typecheck:safe
```

### Quality Checks

```bash
# .NET: format + Release build
make check-server

# Client: biome check (lint + format)
make check-client

# Both
make check-all
```

**Biome Configuration** (`biome.json`):
- **Formatter**: 120 line width, 2 spaces, single quotes, no semicolons
- **Linter**: Recommended rules, strict unused imports
- **Tailwind Sorting**: `useSortedClasses` enabled for automatic Tailwind CSS class ordering
- **Import Organization**: Auto-organize with standard ordering

### Testing

```bash
bun test               # Run all tests
bun run test:watch     # Watch mode
bun run test:coverage  # Coverage report
bun run test:health    # Health check tests only
```

### Database Operations

Database scripts use `dotenv-cli` to load `server/.env` for `DATABASE_URL`.

```bash
bun run db:create      # Create schema
bun run db:seed        # Seed sample data
bun run db:fresh       # Drop, create, seed (full reset)
bun run db:drop        # Drop all tables
bun run db:shell       # Interactive psql shell
bun run db:tables      # List tables
bun run db:run -- path/to/file.sql  # Run custom SQL
```

**Schema Highlights** (`db/schema.sql`):
- TypeID identifiers (`user_`, `item_`, `oauth_`, `sess_`)
- Auto-updating `updated_at` triggers
- Session tracking with user agent and IP
- Performance indexes on common queries
- `cleanup_expired_sessions()` function

## 🔑 API Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/` | GET | No | API info |
| `/health` | GET | No | Health check with memory stats & DB status |
| `/api/auth/register` | POST | No | User registration |
| `/api/auth/login` | POST | No | User login |
| `/api/auth/refresh` | POST | No | Refresh access token |
| `/api/auth/logout` | POST | Yes | Session invalidation |
| `/api/auth/me` | GET | Yes | Current user |
| `/api/auth/sessions` | GET | Yes | List active sessions |
| `/api/items` | GET | Yes | List user's items |
| `/api/items/:id` | GET | Yes | Get item |
| `/api/items` | POST | Yes | Create item |
| `/api/items/:id` | PUT | Yes | Update item |
| `/api/items/:id` | DELETE | Yes | Delete item |

## 🎨 Path Aliases

The client uses TypeScript path aliases for clean imports. Aliases are configured in **both** `tsconfig.app.json` and `vite.config.ts`.

```typescript
import { api } from '@client/api/items'
import { Button } from '@client/components/ui'
```

- `@client/*` → `client/src/*`

**⚠️** When adding new aliases, update both `client/tsconfig.app.json` (paths) and `client/vite.config.ts` (resolve.alias).

## 🛠️ Tech Stack

### Backend
- **.NET 10**: Server runtime and SDK
- **ASP.NET Core Minimal APIs**: HTTP routing and middleware
- **Dapper**: Lightweight data access
- **Npgsql**: PostgreSQL driver
- **Microsoft.AspNetCore.Authentication.JwtBearer**: JWT authentication
- **TypeID**: K-sortable type-safe identifiers

### Frontend
- **React 19**: UI library
- **Vite 7**: Build tool and dev server
- **Tailwind CSS 4**: Utility-first CSS with Vite plugin
- **tsgo**: TypeScript 7 native compiler for type-checking

### Database
- **PostgreSQL 16**: Relational database with triggers and TypeID

### Tooling
- **Bun 1.3.0**: Package manager and test runner
- **Biome**: Linter and formatter with Tailwind class sorting
- **dotnet format**: C# formatting and style checks
- **Concurrently**: Parallel dev servers

### DevOps
- **Docker**: Multi-stage builds (.NET SDK → ASP.NET runtime, Bun → Nginx)
- **Nginx**: Production web server for client
- **Kubernetes**: Full K8s manifests with Makefile automation

## 🐳 Docker Deployment

This template uses **GitHub Container Registry (GHCR)** for Docker images. PostgreSQL is managed separately (local install, managed service, or separate container).

See [DOCKER.md](./DOCKER.md) for the comprehensive deployment guide.

### Building and Pushing Images

```bash
# Set credentials
export GITHUB_USER=your-github-username
export GITHUB_TOKEN=ghp_your_personal_access_token

# Full deployment workflow (login + build + push)
make deploy

# Or step by step
make login            # Login to GHCR
make docker-build-all # Build server + client images
make push-all         # Push both images
```

### Deploying on VPS

```bash
# Copy deployment files to VPS
scp .env.production docker-compose.yml your-vps:/path/to/deploy/

# On VPS: configure .env.production with your DATABASE_URL, JWT_SECRET, etc.
# Then pull and start
docker compose --env-file .env.production pull
docker compose --env-file .env.production up -d
```

**Runtime Configuration**: The client reads `VITE_API_URL` at container startup (not build time). Change it in `.env.production` and restart the container — no rebuild needed.

### ☸️ Kubernetes Deployment

See [KUBERNETES.md](./KUBERNETES.md) for complete K8s deployment with:
- Deployments, services, configmaps, secrets, ingress
- 40+ Makefile commands for K8s management
- Scaling, monitoring, and troubleshooting guides

## 📚 Documentation

- [ENV_VARS.md](./ENV_VARS.md) — Environment variables reference
- [TEMPLATE.md](./TEMPLATE.md) — How to use this as a project template
- [DOCKER.md](./DOCKER.md) — Docker deployment guide
- [KUBERNETES.md](./KUBERNETES.md) — Kubernetes deployment guide
- [QUICK_START.md](./QUICK_START.md) — Quick reference guide

## 📄 License

MIT License — feel free to use this template for any purpose.
