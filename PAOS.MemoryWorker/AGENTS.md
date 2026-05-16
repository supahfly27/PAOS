# PAOS.MemoryWorker

## Related AGENTS.md — read only if your task involves that area
| File | Read when… |
|------|-----------|
| `../AGENTS.md` | need system overview, Docker setup, or env key reference |
| `../PAOS.Data/AGENTS.md` | need `MemoryEmbedding` fields or vector column constraints |
| `../PAOS.MemoryAPI/AGENTS.md` | checking which endpoints push to the embed queue |

.NET 10 Worker Service. Single hosted service: `EmbeddingWorker`. Runs as `memory_worker` Docker container.

## Services Registered (Program.cs)
```
MemoryDbContext         → Npgsql (ConnectionStrings:Postgres)
IConnectionMultiplexer  → StackExchange.Redis (ConnectionStrings:Redis)
EmbeddingWorker         → IHostedService
```

## EmbeddingWorker (`Workers/EmbeddingWorker.cs`)

**Lifecycle:** Polls Redis `embed_queue` every 2 seconds. Exits gracefully on `CancellationToken`.

**Startup guard:** If `OpenAI:ApiKey` is not configured, logs a warning and returns immediately (no-op, no crash).

**Job format** (JSON string in Redis list):
```json
{"EntityType":"Source","EntityId":"<guid>","Text":"<text to embed>"}
```
Deserialized to `record EmbedJob(string EntityType, string EntityId, string Text)`.

**Processing loop:**
1. `db.ListLeftPopAsync("embed_queue")` — non-blocking; sleeps 2s if empty
2. Deserialize job
3. `EmbeddingClient("text-embedding-3-small", apiKey).GenerateEmbeddingAsync(job.Text)`
4. Format vector: `[f1,f2,...,f1536]` (G7 format, invariant culture)
5. Open fresh `NpgsqlConnection` from `MemoryDbContext.Database.GetConnectionString()`
6. Raw SQL insert (bypasses EF because `Embedding` is `[NotMapped]`):
   ```sql
   INSERT INTO "MemoryEmbeddings" ("Id","EntityType","EntityId","CreatedAt","Embedding")
   VALUES (@id, @entityType, @entityId, NOW(), @embedding::vector)
   ON CONFLICT DO NOTHING
   ```
7. Log `Embedded {EntityType}/{EntityId}`

**Error handling:** Any exception logs the error and sleeps 5 seconds before retrying.

## Adding New Embed Sources
To queue embeddings from a new endpoint, inject `IConnectionMultiplexer` and push:
```csharp
var job = JsonSerializer.Serialize(new { entityType = "Fact", entityId = fact.Id.ToString(), text = $"{fact.Subject} {fact.Predicate} {fact.Object}" });
await redis.GetDatabase().ListRightPushAsync("embed_queue", job);
```
Queue key is always `"embed_queue"`. Worker uses `ListLeftPopAsync` (FIFO).

## Configuration Keys
```
ConnectionStrings__Postgres   required
ConnectionStrings__Redis       required
OpenAI__ApiKey                 required for embedding; worker silently skips if absent
```
