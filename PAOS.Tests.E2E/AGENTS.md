# PAOS.Tests.E2E

## Related AGENTS.md — read only if your task involves that area
| File | Read when… |
|------|-----------|
| `../AGENTS.md` | need system overview, Docker setup, or env key reference |
| `../PAOS.MemoryAPI/AGENTS.md` | need endpoint signatures, request shapes, or response contracts |
| `../PAOS.Data/AGENTS.md` | need entity field names or FK relationships for cleanup order |
| `../PAOS.Tests/AGENTS.md` | need in-process integration tests (WebApplicationFactory pattern) |

xUnit **full-stack E2E** test project. Hits the real Docker API at `http://localhost:8000`. Verifies data in Postgres, Redis `embed_queue`, and MinIO. All 5 Docker services must be running (`docker compose up --build`).

**Key difference from `PAOS.Tests`:** No `WebApplicationFactory` — tests send real HTTP to the running Docker container. Slower but covers the full stack including Docker networking, MinIO, and the EmbeddingWorker as a separate process.

## Infrastructure

**`E2EFixture.cs`** — shared fixture (one instance per test run via `ICollectionFixture`):
```csharp
public const string ApiBase        = "http://localhost:8000";
public const string PostgresConnString = "Host=localhost;Port=5432;Database=agentic_os;Username=agent;Password=agent_password";
public const string RedisConnString    = "localhost:6379";
public const string MinioServiceUrl    = "http://localhost:9000";
public const string MinioAccessKey     = "minio";
public const string MinioSecretKey     = "minio_password";
public const string MinioBucket        = "paos-e2e-test";
```
Properties: `Http` (HttpClient), `Db` (NpgsqlDataSource), `Redis` (IDatabase), `S3` (AmazonS3Client).
`InitializeAsync` connects Redis and ensures the MinIO bucket exists.

**`E2ECollection.cs`** — declares `[CollectionDefinition("E2E")]` with `ICollectionFixture<E2EFixture>`.
All test classes use `[Collection("E2E")]`:
- One fixture shared across all classes
- Tests run **sequentially** (no parallel DB conflicts)

**`Helpers/CleanupHelper.cs`**:
- `DeleteTablesAsync(db, params string[] tables)` — runs `DELETE FROM "Table"` for each name in order
- `CountRowsAsync(db, table, whereClause, params NpgsqlParameter[])` — returns `long` row count for assertions

## Test Class Pattern

```csharp
[Collection("E2E")]
public class XxxE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db, "AuditLogs", "ChildTable", "ParentTable");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostX_Returns201AndAppearsInDB()
    {
        var res = await f.Http.PostAsJsonAsync("/x", new { ... });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "TableX", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }
}
```

## Test Files

| File | Domain | Tables Cleaned |
|------|--------|---------------|
| `Sources/SourcesE2ETests.cs` | Sources | AuditLogs, MemoryEmbeddings, SourceChunks, Sources |
| `Search/SearchE2ETests.cs` | Search | AuditLogs, MemoryEmbeddings, SourceChunks, Sources |
| `Identity/IdentityE2ETests.cs` | Identity | AuditLogs, UserPreferences, UserGoals, UserValues, UserHabits, UserProfiles |
| `People/PeopleE2ETests.cs` | People | AuditLogs, Promises, PersonFacts, Interactions, People |
| `Projects/ProjectsE2ETests.cs` | Projects | AuditLogs, ProjectFiles, ProjectStatusUpdates, ProjectBlockers, ProjectMembers, ProjectEvents, Projects |
| `Commitments/CommitmentsE2ETests.cs` | Commitments | AuditLogs, CommitmentStatusHistories, CommitmentSources, Commitments, SourceChunks, Sources |
| `Episodic/EpisodicE2ETests.cs` | Events | AuditLogs, EventSummaries, EventSources, EventParticipants, MemoryEvents, Sources, People |
| `Semantic/SemanticE2ETests.cs` | Facts | AuditLogs, FactConfidenceHistories, FactSources, Facts |
| `Procedural/ProceduralE2ETests.cs` | Procedures | AuditLogs, ProcedureFeedback, ProcedureRuns, ProcedureSteps, Procedures |
| `Decisions/DecisionsE2ETests.cs` | Decisions | AuditLogs, DecisionOutcomes, DecisionAssumptions, DecisionOptions, Decisions |

## Special Test Behaviours

**Redis embed queue** (`SourcesE2ETests`): `PostSource_PushesJobToRedisOrEmbeddingAppears` checks that `embed_queue` LLEN increased OR a `MemoryEmbeddings` row appeared (handles the case where the worker consumed the job before the assertion runs). Clears the queue key in `InitializeAsync`.

**Embedding pipeline** (`SourcesE2ETests`): `EmbeddingPipeline_EventuallyCreatesRow_WhenApiKeyConfigured` — skips silently if `OPENAI__APIKEY` / `OpenAI__ApiKey` env var is absent; otherwise polls `MemoryEmbeddings` every 2s for up to 30s.

**MinIO** (`ProjectsE2ETests`): `MinioUpload_FileExistsInBucket` uploads a file via `f.S3.PutObjectAsync`, asserts `GetObjectMetadataAsync` returns 200, then deletes the object. Tests that MinIO is reachable and writable, independent of the API.

**Semantic search without API key** (`SearchE2ETests`): `SemanticSearch_Returns503_WhenNoApiKey` — skips silently if a key IS set (the API would succeed, not 503).

## Running Tests

```bash
# All 5 Docker services must be running first
docker compose up --build

dotnet test PAOS.Tests.E2E/PAOS.Tests.E2E.csproj

# Currently: 40 tests, 0 failures (with Docker stack running)
```

## Adding a New Test Class
1. `[Collection("E2E")]` + `IAsyncLifetime` — mandatory; omitting causes parallel execution
2. `InitializeAsync` must delete in FK-safe order (children before parents, always include `AuditLogs`)
3. Use `f.Http`, `f.Db`, `f.Redis`, `f.S3` from the fixture — do not create new connections
4. For DB assertions use `CleanupHelper.CountRowsAsync`, not the API response alone
