# PAOS.MemoryAPI

## Related AGENTS.md — read only if your task involves that area
| File | Read when… |
|------|-----------|
| `../AGENTS.md` | need system overview, Docker setup, or cross-cutting conventions |
| `../PAOS.Data/AGENTS.md` | need entity fields, nav properties, or table names |
| `../PAOS.MemoryWorker/AGENTS.md` | modifying embed queue format or worker behaviour |
| `../PAOS.Tests/AGENTS.md` | adding or fixing integration tests for these endpoints |

ASP.NET Core 10 Web API using minimal APIs. All endpoints registered in `Program.cs` via extension methods. Listens on port 8080 (8000 externally via Docker).

## Program.cs Services
```
MemoryDbContext         → Npgsql (ConnectionStrings:Postgres)
IConnectionMultiplexer  → StackExchange.Redis (ConnectionStrings:Redis)
HealthChecks            → Npgsql + Redis
HttpJsonOptions         → ReferenceHandler.IgnoreCycles  ← required; EF nav properties cause cycles
```

## Endpoint Files (`Endpoints/`)

| File | Route Group | Endpoints |
|------|------------|-----------|
| `HealthEndpoints.cs` | `/` | `GET /` → `{service, status}` |
| `SourceEndpoints.cs` | `/sources` | `POST /`, `GET /{id}` |
| `IdentityEndpoints.cs` | `/identity` | `POST /`, `GET /{id}`, `POST /{id}/preferences`, `POST /{id}/goals`, `PUT /{id}/goals/{goalId}`, `DELETE /{id}` |
| `PeopleEndpoints.cs` | `/people` | `POST /`, `GET /{id}`, `POST /{id}/interactions`, `POST /{id}/facts`, `POST /{id}/promises`, `GET /{id}/promises`, `PUT /{id}/promises/{promiseId}` |
| `ProjectEndpoints.cs` | `/projects` | `POST /`, `GET /{id}`, `POST /{id}/members`, `POST /{id}/blockers`, `PUT /{id}/blockers/{blockerId}/resolve`, `POST /{id}/status`, `POST /{id}/files` |
| `CommitmentEndpoints.cs` | `/commitments` | `POST /`, `GET /`, `GET /{id}`, `PUT /{id}/status` |
| `EpisodicEndpoints.cs` | `/events` | `POST /`, `GET /{id}`, `POST /{id}/participants`, `POST /{id}/sources`, `POST /{id}/summaries` |
| `SemanticEndpoints.cs` | `/facts` | `POST /`, `GET /`, `GET /{id}`, `PUT /{id}/confidence` |
| `ProceduralEndpoints.cs` | `/procedures` | `POST /`, `GET /{id}`, `POST /{id}/steps`, `POST /{id}/runs`, `PUT /{id}/runs/{runId}/complete`, `POST /{id}/runs/{runId}/feedback` |
| `DecisionEndpoints.cs` | `/decisions` | `POST /`, `GET /{id}`, `POST /{id}/options`, `POST /{id}/assumptions`, `PUT /{id}/assumptions/{assumptionId}/invalidate`, `POST /{id}/outcomes` |
| `SearchEndpoints.cs` | `/search` | `POST /` (semantic), `POST /keyword` (ILike) |

## Endpoint Conventions

**Every mutation** writes an `AuditLog` row (`EntityType`, `EntityId`, `Action`, `ChangedBy="api"`) in the same `SaveChangesAsync` call.

**Source linking** (where supported): accept `Guid[]? SourceIds` in the request, create a junction row per ID (`CommitmentSource`, `FactSource`, `EventSource`).

**Response shapes:**
- `POST` → `201 Created` with `{ id: Guid }` body
- `GET /{id}` → `200 OK` with full EF entity (nav props included) or `404`
- `PUT` → `200 OK` with updated scalar fields
- `DELETE` → `204 No Content` (soft delete: sets `UpdatedAt`, writes audit log, no DB row removal)

**List endpoints** support optional query-string filters (e.g. `?status=open`, `?subject=Alice`).

## Embedding Queue
`POST /sources` pushes a job to Redis `embed_queue` after save (via `IConnectionMultiplexer`):
```json
{"entityType":"Source","entityId":"<guid>","text":"<rawContent>"}
```
`EmbeddingWorker` (PAOS.MemoryWorker) consumes this queue.

## Search Endpoints Detail

**`POST /search`** (semantic)
- Requires `OpenAI:ApiKey` in config; returns `503` if missing
- Calls `EmbeddingClient("text-embedding-3-small")` → formats vector as `[f1,f2,...]`
- Opens fresh `NpgsqlConnection` from `db.Database.GetConnectionString()`
- Raw SQL: `SELECT "EntityType","EntityId", 1-("Embedding"<=>@queryVec::vector) AS score FROM "MemoryEmbeddings" ORDER BY "Embedding"<=>@queryVec::vector LIMIT @limit`
- Returns `[{entityType, entityId, score}]`

**`POST /search/keyword`**
- `EF.Functions.ILike(s.RawContent, $"%{req.Query}%")` on `Sources`
- Returns `[{id, type, rawContent}]`

## Configuration Keys (via .env / environment)
```
ConnectionStrings__Postgres   required
ConnectionStrings__Redis       required
OpenAI__ApiKey                 optional — semantic search returns 503 without it
```
