# PAOS Memory Layer — Solution Overview

## What This Is
Personal Agentic OS memory subsystem. Ingests raw evidence, stores structured memories across 10 domain types, embeds them via OpenAI, and retrieves semantically via pgvector.

## Projects

| Project | Type | Purpose |
|---------|------|---------|
| `PAOS.Data` | Class library | EF Core entities, DbContext, all migrations |
| `PAOS.MemoryAPI` | ASP.NET Core Web API | REST endpoints, runs on port 8000 (Docker) / 8080 (local) |
| `PAOS.MemoryWorker` | .NET Worker Service | Background embedding pipeline |
| `PAOS.Tests` | xUnit | Integration tests against live Postgres + Redis |
| `PAOS.Models` | Class library | Stub — unused, do not add DTOs here (use inline records in endpoints) |
| `PAOS.Console` | Console app | Stub — not part of memory layer |

## Infrastructure (docker-compose.yml)

| Service | Image | Port | Role |
|---------|-------|------|------|
| `memory_postgres` | pgvector/pgvector:pg16 | 5432 | Primary store + vector search |
| `memory_redis` | redis:7 | 6379 | Embed job queue (`embed_queue`) |
| `memory_minio` | minio/minio | 9000/9001 | File/evidence storage (S3-compatible) |
| `memory_api` | built from `PAOS.MemoryAPI/Dockerfile` | 8000→8080 | REST API |
| `memory_worker` | built from `PAOS.MemoryWorker/Dockerfile` | — | EmbeddingWorker |

## Runtime Configuration

All services read from `.env` (gitignored). Required keys:
```
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=agentic_os;Username=agent;Password=agent_password
ConnectionStrings__Redis=redis:6379
OpenAI__ApiKey=sk-...
Minio__Endpoint=minio:9000
Minio__AccessKey=minio
Minio__SecretKey=minio_password
```
Local dev defaults: Postgres on `localhost:5432`, Redis on `localhost:6379` (hardcoded in `ApiFactory` and `MemoryDbContextFactory`).

## Tech Stack
- .NET 10 / C# 13
- EF Core 9 + Npgsql 9 (pgvector support automatic — no `UseVector()` needed)
- StackExchange.Redis 2.x
- OpenAI .NET SDK 2.x (`EmbeddingClient`, model: `text-embedding-3-small`, 1536 dims)
- pgvector operator `<=>` for cosine distance

## Embedding Flow
1. `POST /sources` saves source → pushes JSON job to Redis `embed_queue`
2. `EmbeddingWorker` pops job → calls OpenAI → inserts vector row via raw Npgsql into `MemoryEmbeddings`
3. `POST /search` generates query embedding → raw Npgsql cosine distance query → returns `[{entityType, entityId, score}]`

## Dev Commands
```bash
docker compose up --build          # start all 5 services
dotnet ef migrations add <Name> --project PAOS.Data --startup-project PAOS.Data
dotnet ef database update          --project PAOS.Data --startup-project PAOS.Data
dotnet test PAOS.Tests/PAOS.Tests.csproj   # requires Docker Postgres + Redis running
```

## Conventions
- Every mutation endpoint writes an `AuditLog` row in the same `SaveChangesAsync` call
- `ChangedBy` is always `"api"` (no auth yet)
- All IDs are `Guid`, auto-generated via `Guid.NewGuid()` in entity constructors
- Request/response types are C# `record`s defined at the bottom of each endpoint file
- No DTOs in `PAOS.Models` — use inline records
- JSON serialization uses `ReferenceHandler.IgnoreCycles` (EF nav properties cause cycles)
