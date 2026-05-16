# PAOS.Tests

## Related AGENTS.md — read only if your task involves that area
| File | Read when… |
|------|-----------|
| `../AGENTS.md` | need system overview or dev environment setup |
| `../PAOS.MemoryAPI/AGENTS.md` | need endpoint signatures, request shapes, or response contracts |
| `../PAOS.Data/AGENTS.md` | need entity field names or FK relationships for cleanup order |

xUnit integration test project. Tests run against live Postgres and Redis (Docker must be running).

## Infrastructure

**`ApiFactory.cs`** — `WebApplicationFactory<Program>` subclass.
- Static constructor sets env vars before `WebApplication.CreateBuilder` reads config:
  ```csharp
  ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=agentic_os;Username=agent;Password=agent_password"
  ConnectionStrings__Redis    = "localhost:6379"
  ```
- `UseEnvironment("Test")` — no other behaviour changes needed.

**`IntegrationCollection.cs`** — declares `[CollectionDefinition("Integration")]` with `ICollectionFixture<ApiFactory>`.
All test classes use `[Collection("Integration")]`, which means:
- One `ApiFactory` instance shared across all test classes
- Tests run **sequentially** (no parallel DB conflicts)

## Test Class Pattern

Every test class follows this exact structure:

```csharp
[Collection("Integration")]
public class XxxEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        // Delete dependent tables first, then parents (FK order)
        db.ChildTable.RemoveRange(db.ChildTable);
        db.ParentTable.RemoveRange(db.ParentTable);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostX_Returns201WithId() { ... }
}
```

**Critical:** `InitializeAsync` cleans only the tables relevant to that test class, in FK-safe deletion order (children before parents, always include `AuditLogs`).

## Test Files

| File | Domain | Tables Cleaned |
|------|--------|---------------|
| `Sources/SourceEndpointsTests.cs` | Sources | AuditLogs, SourceChunks, Sources |
| `Identity/IdentityEndpointsTests.cs` | Identity | AuditLogs, UserPreferences, UserGoals, UserValues, UserHabits, UserProfiles |
| `People/PeopleEndpointsTests.cs` | People | AuditLogs, Promises, PersonFacts, Interactions, People |
| `Projects/ProjectEndpointsTests.cs` | Projects | AuditLogs, ProjectFiles, ProjectStatusUpdates, ProjectBlockers, ProjectMembers, ProjectEvents, Projects |
| `Commitments/CommitmentEndpointsTests.cs` | Commitments | AuditLogs, CommitmentStatusHistories, CommitmentSources, Commitments, SourceChunks, Sources |
| `Episodic/EpisodicEndpointsTests.cs` | Events | AuditLogs, EventSummaries, EventSources, EventParticipants, MemoryEvents |
| `Semantic/SemanticEndpointsTests.cs` | Facts | AuditLogs, FactConfidenceHistories, FactSources, Facts |
| `Procedural/ProceduralEndpointsTests.cs` | Procedures | AuditLogs, ProcedureFeedback, ProcedureRuns, ProcedureSteps, Procedures |
| `Decisions/DecisionEndpointsTests.cs` | Decisions | AuditLogs, DecisionOutcomes, DecisionAssumptions, DecisionOptions, Decisions |
| `Search/SearchEndpointsTests.cs` | Search | AuditLogs, MemoryEmbeddings, SourceChunks, Sources |

## Running Tests
```bash
# Docker Postgres + Redis must be running first
dotnet test PAOS.Tests/PAOS.Tests.csproj

# Currently: 53 tests, 0 failures
```

## Semantic Search Tests
`SemanticSearch_WithoutApiKey_Returns503` — skips automatically if `OPENAI__APIKEY` or `OpenAI__ApiKey` env var is set (assumes real API call would be made instead).

## Adding a New Test Class
1. Mirror the cleanup pattern exactly — wrong FK order will cause `SaveChangesAsync` to throw
2. Add `[Collection("Integration")]` — omitting this causes parallel execution and flaky tests
3. Use `factory.CreateClient()` — do not instantiate `HttpClient` directly
