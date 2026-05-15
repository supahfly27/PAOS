# PAOS Memory Layer — Master Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Memory Layer foundation for the Personal Agentic OS — ingest, classify, link, embed, retrieve semantically, and show evidence for all memory types.

**Architecture:** Five-service system containerized via Docker Compose: PostgreSQL+pgvector (structured data + vector search), Redis (short-term memory + job queues), MinIO (raw evidence files), a REST API (PAOS.MemoryAPI), and a background worker (PAOS.MemoryWorker). All services are .NET 10, sharing domain entities via PAOS.Data.

**Tech Stack:** .NET 10 · C# 13 · ASP.NET Core Web API · .NET Worker Service · Entity Framework Core 9 · Npgsql + pgvector-dotnet · StackExchange.Redis · AWSSDK.S3 (MinIO-compatible) · OpenAI text-embedding-3-small (1536 dims) · PostgreSQL 16 + pgvector · Docker Compose

---

## Solution Structure

```
Solution1/
├── PAOS.Data/                   # EF Core DbContext, all entities, all migrations
│   ├── PAOS.Data.csproj
│   ├── MemoryDbContext.cs
│   ├── Entities/                # One file per aggregate root
│   │   ├── Identity/
│   │   ├── People/
│   │   ├── Projects/
│   │   ├── Commitments/
│   │   ├── Episodic/
│   │   ├── Semantic/
│   │   ├── Procedural/
│   │   ├── Decisions/
│   │   ├── Sources/
│   │   └── Search/
│   └── Migrations/
│
├── PAOS.MemoryAPI/              # ASP.NET Core Web API (minimal API style)
│   ├── PAOS.MemoryAPI.csproj
│   ├── Program.cs
│   └── Endpoints/               # One file per domain
│       ├── HealthEndpoints.cs
│       ├── SourceEndpoints.cs
│       ├── IdentityEndpoints.cs
│       ├── PeopleEndpoints.cs
│       ├── ProjectEndpoints.cs
│       ├── CommitmentEndpoints.cs
│       └── SearchEndpoints.cs
│
├── PAOS.MemoryWorker/           # .NET Worker Service
│   ├── PAOS.MemoryWorker.csproj
│   ├── Program.cs
│   └── Workers/
│       └── EmbeddingWorker.cs   # Polls Redis queue, generates embeddings
│
├── PAOS.Models/                 # Lightweight shared DTOs/request-response types
│   └── PAOS.Models.csproj
│
├── docker-compose.yml           # All 5 infrastructure services
├── docker-compose.override.yml  # Dev overrides
└── .env                         # Connection strings, API keys
```

---

## Phase 1: Infrastructure

**Goal:** `docker compose up` → all 5 services healthy, API returns 200 on `/health`.

**Milestone:** No code logic, just wiring. Every service starts, connects, and can be pinged.

---

### Task 1.1 — Update docker-compose.yml

**Files:**
- Modify: `docker-compose.yml`
- Modify: `docker-compose.override.yml`
- Create: `.env`

- [ ] **Replace docker-compose.yml with full service definitions:**

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    container_name: memory_postgres
    environment:
      POSTGRES_DB: agentic_os
      POSTGRES_USER: agent
      POSTGRES_PASSWORD: agent_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U agent -d agentic_os"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7
    container_name: memory_redis
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 5s
      retries: 5

  minio:
    image: minio/minio
    container_name: memory_minio
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minio
      MINIO_ROOT_PASSWORD: minio_password
    ports:
      - "9000:9000"
      - "9001:9001"
    volumes:
      - minio_data:/data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 10s
      timeout: 5s
      retries: 5

  memory_api:
    build:
      context: .
      dockerfile: PAOS.MemoryAPI/Dockerfile
    container_name: memory_api
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      minio:
        condition: service_healthy
    ports:
      - "8000:8080"
    env_file:
      - .env

  memory_worker:
    build:
      context: .
      dockerfile: PAOS.MemoryWorker/Dockerfile
    container_name: memory_worker
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      minio:
        condition: service_healthy
    env_file:
      - .env

volumes:
  postgres_data:
  redis_data:
  minio_data:
```

- [ ] **Create .env with connection strings:**

```env
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=agentic_os;Username=agent;Password=agent_password
ConnectionStrings__Redis=redis:6379
Minio__Endpoint=minio:9000
Minio__AccessKey=minio
Minio__SecretKey=minio_password
Minio__BucketName=memory-evidence
OpenAI__ApiKey=sk-YOUR_KEY_HERE
```

- [ ] **Commit:**

```bash
git add docker-compose.yml docker-compose.override.yml .env
git commit -m "feat: full docker-compose with postgres, redis, minio, api, worker"
```

---

### Task 1.2 — Create PAOS.Data Project

**Files:**
- Create: `PAOS.Data/PAOS.Data.csproj`
- Create: `PAOS.Data/MemoryDbContext.cs`

- [ ] **Scaffold project from Solution1 directory:**

```bash
dotnet new classlib -n PAOS.Data -f net10.0
dotnet sln PAOS.slnx add PAOS.Data/PAOS.Data.csproj
```

- [ ] **Replace PAOS.Data/PAOS.Data.csproj:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.*" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
    <PackageReference Include="pgvector" Version="0.*" />
  </ItemGroup>
</Project>
```

- [ ] **Create PAOS.Data/MemoryDbContext.cs (empty, fills out in Phase 2):**

```csharp
using Microsoft.EntityFrameworkCore;

namespace PAOS.Data;

public class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Commit:**

```bash
git add PAOS.Data/
git commit -m "feat: scaffold PAOS.Data project with EF Core + pgvector"
```

---

### Task 1.3 — Create PAOS.MemoryAPI Project

**Files:**
- Create: `PAOS.MemoryAPI/PAOS.MemoryAPI.csproj`
- Create: `PAOS.MemoryAPI/Program.cs`
- Create: `PAOS.MemoryAPI/Endpoints/HealthEndpoints.cs`
- Create: `PAOS.MemoryAPI/Dockerfile`

- [ ] **Scaffold project:**

```bash
dotnet new webapi -n PAOS.MemoryAPI -f net10.0 --use-minimal-apis
dotnet sln PAOS.slnx add PAOS.MemoryAPI/PAOS.MemoryAPI.csproj
dotnet add PAOS.MemoryAPI/PAOS.MemoryAPI.csproj reference PAOS.Data/PAOS.Data.csproj
```

- [ ] **Replace PAOS.MemoryAPI/PAOS.MemoryAPI.csproj:**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
    <PackageReference Include="StackExchange.Redis" Version="2.*" />
    <PackageReference Include="AWSSDK.S3" Version="3.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\PAOS.Data\PAOS.Data.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Write PAOS.MemoryAPI/Program.cs:**

```csharp
using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.MemoryAPI.Endpoints;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseVector()));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthEndpoints();

app.Run();
```

- [ ] **Write PAOS.MemoryAPI/Endpoints/HealthEndpoints.cs:**

```csharp
namespace PAOS.MemoryAPI.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new { service = "PAOS Memory API", status = "running" }));
    }
}
```

- [ ] **Write PAOS.MemoryAPI/Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PAOS.MemoryAPI/PAOS.MemoryAPI.csproj", "PAOS.MemoryAPI/"]
COPY ["PAOS.Data/PAOS.Data.csproj", "PAOS.Data/"]
RUN dotnet restore "PAOS.MemoryAPI/PAOS.MemoryAPI.csproj"
COPY . .
RUN dotnet publish "PAOS.MemoryAPI/PAOS.MemoryAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PAOS.MemoryAPI.dll"]
```

- [ ] **Commit:**

```bash
git add PAOS.MemoryAPI/
git commit -m "feat: scaffold PAOS.MemoryAPI with health endpoint"
```

---

### Task 1.4 — Create PAOS.MemoryWorker Project

**Files:**
- Create: `PAOS.MemoryWorker/PAOS.MemoryWorker.csproj`
- Create: `PAOS.MemoryWorker/Program.cs`
- Create: `PAOS.MemoryWorker/Workers/EmbeddingWorker.cs`
- Create: `PAOS.MemoryWorker/Dockerfile`

- [ ] **Scaffold project:**

```bash
dotnet new worker -n PAOS.MemoryWorker -f net10.0
dotnet sln PAOS.slnx add PAOS.MemoryWorker/PAOS.MemoryWorker.csproj
dotnet add PAOS.MemoryWorker/PAOS.MemoryWorker.csproj reference PAOS.Data/PAOS.Data.csproj
```

- [ ] **Write PAOS.MemoryWorker/Program.cs:**

```csharp
using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.MemoryWorker.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseVector()));

builder.Services.AddHostedService<EmbeddingWorker>();

var host = builder.Build();
host.Run();
```

- [ ] **Write PAOS.MemoryWorker/Workers/EmbeddingWorker.cs (stub):**

```csharp
namespace PAOS.MemoryWorker.Workers;

public class EmbeddingWorker(ILogger<EmbeddingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EmbeddingWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            // Phase 4: poll Redis queue and generate embeddings
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

- [ ] **Write PAOS.MemoryWorker/Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PAOS.MemoryWorker/PAOS.MemoryWorker.csproj", "PAOS.MemoryWorker/"]
COPY ["PAOS.Data/PAOS.Data.csproj", "PAOS.Data/"]
RUN dotnet restore "PAOS.MemoryWorker/PAOS.MemoryWorker.csproj"
COPY . .
RUN dotnet publish "PAOS.MemoryWorker/PAOS.MemoryWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PAOS.MemoryWorker.dll"]
```

- [ ] **Commit:**

```bash
git add PAOS.MemoryWorker/
git commit -m "feat: scaffold PAOS.MemoryWorker with stub EmbeddingWorker"
```

---

### Task 1.5 — Phase 1 Verification

- [ ] **Run `docker compose up --build` — all 5 containers must start**
- [ ] **Hit `http://localhost:8000/health` — expect 200 with `{"status":"Healthy"}`**
- [ ] **Hit `http://localhost:8000/` — expect `{"service":"PAOS Memory API","status":"running"}`**
- [ ] **Open MinIO console at `http://localhost:9001` — login minio/minio_password**

**Phase 1 complete when:** All 4 checks pass with zero container restarts.

---

## Phase 2: Foundation Schema + Source/Evidence

**Goal:** Every DB table exists. A raw text payload can be POSTed, stored with full source provenance, and returned by ID.

**Milestone:** `POST /sources` → stored in Postgres with audit log → `GET /sources/{id}` returns it with source metadata.

**Why Source/Evidence first:** Every memory type references a source. Build this before any domain features.

---

### Task 2.1 — Define All EF Core Entities

**Files:**
- Create: `PAOS.Data/Entities/Sources/Source.cs`
- Create: `PAOS.Data/Entities/Sources/SourceChunk.cs`
- Create: `PAOS.Data/Entities/Sources/AuditLog.cs`
- Create: `PAOS.Data/Entities/Identity/UserProfile.cs`
- Create: `PAOS.Data/Entities/Identity/UserPreference.cs`
- Create: `PAOS.Data/Entities/Identity/UserGoal.cs`
- Create: `PAOS.Data/Entities/Identity/UserValue.cs`
- Create: `PAOS.Data/Entities/Identity/UserHabit.cs`
- Create: `PAOS.Data/Entities/People/Person.cs`
- Create: `PAOS.Data/Entities/People/Organization.cs`
- Create: `PAOS.Data/Entities/People/Relationship.cs`
- Create: `PAOS.Data/Entities/People/Interaction.cs`
- Create: `PAOS.Data/Entities/People/PersonFact.cs`
- Create: `PAOS.Data/Entities/People/Promise.cs`
- Create: `PAOS.Data/Entities/Projects/Project.cs`
- Create: `PAOS.Data/Entities/Projects/ProjectMember.cs`
- Create: `PAOS.Data/Entities/Projects/ProjectEvent.cs`
- Create: `PAOS.Data/Entities/Projects/ProjectBlocker.cs`
- Create: `PAOS.Data/Entities/Projects/ProjectFile.cs`
- Create: `PAOS.Data/Entities/Projects/ProjectStatusUpdate.cs`
- Create: `PAOS.Data/Entities/Commitments/Commitment.cs`
- Create: `PAOS.Data/Entities/Commitments/CommitmentSource.cs`
- Create: `PAOS.Data/Entities/Commitments/CommitmentStatusHistory.cs`
- Create: `PAOS.Data/Entities/Episodic/Event.cs`
- Create: `PAOS.Data/Entities/Episodic/EventParticipant.cs`
- Create: `PAOS.Data/Entities/Episodic/EventSource.cs`
- Create: `PAOS.Data/Entities/Episodic/EventSummary.cs`
- Create: `PAOS.Data/Entities/Semantic/Fact.cs`
- Create: `PAOS.Data/Entities/Semantic/FactSource.cs`
- Create: `PAOS.Data/Entities/Semantic/FactConflict.cs`
- Create: `PAOS.Data/Entities/Semantic/FactConfidenceHistory.cs`
- Create: `PAOS.Data/Entities/Procedural/Procedure.cs`
- Create: `PAOS.Data/Entities/Procedural/ProcedureStep.cs`
- Create: `PAOS.Data/Entities/Procedural/ProcedureRun.cs`
- Create: `PAOS.Data/Entities/Procedural/ProcedureFeedback.cs`
- Create: `PAOS.Data/Entities/Decisions/Decision.cs`
- Create: `PAOS.Data/Entities/Decisions/DecisionOption.cs`
- Create: `PAOS.Data/Entities/Decisions/DecisionAssumption.cs`
- Create: `PAOS.Data/Entities/Decisions/DecisionOutcome.cs`
- Create: `PAOS.Data/Entities/Search/MemoryEmbedding.cs`
- Create: `PAOS.Data/Entities/Search/SearchLog.cs`
- Create: `PAOS.Data/Entities/Search/RetrievalFeedback.cs`

- [ ] **Write Source entities:**

```csharp
// PAOS.Data/Entities/Sources/Source.cs
namespace PAOS.Data.Entities.Sources;

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;      // text, file, email, transcript
    public string RawContent { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1.0f;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public ICollection<SourceChunk> Chunks { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Sources/SourceChunk.cs
namespace PAOS.Data.Entities.Sources;

public class SourceChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
}
```

```csharp
// PAOS.Data/Entities/Sources/AuditLog.cs
namespace PAOS.Data.Entities.Sources;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;    // created, updated, deleted
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string DiffJson { get; set; } = "{}";
}
```

- [ ] **Write Identity entities:**

```csharp
// PAOS.Data/Entities/Identity/UserProfile.cs
namespace PAOS.Data.Entities.Identity;

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public string CommunicationStyle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserPreference> Preferences { get; set; } = [];
    public ICollection<UserGoal> Goals { get; set; } = [];
    public ICollection<UserValue> Values { get; set; } = [];
    public ICollection<UserHabit> Habits { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Identity/UserPreference.cs
namespace PAOS.Data.Entities.Identity;

public class UserPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// PAOS.Data/Entities/Identity/UserGoal.cs
namespace PAOS.Data.Entities.Identity;

public class UserGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string Goal { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// PAOS.Data/Entities/Identity/UserValue.cs
namespace PAOS.Data.Entities.Identity;

public class UserValue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string ValueName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

```csharp
// PAOS.Data/Entities/Identity/UserHabit.cs
namespace PAOS.Data.Entities.Identity;

public class UserHabit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string Habit { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
}
```

- [ ] **Write People entities:**

```csharp
// PAOS.Data/Entities/People/Person.cs
namespace PAOS.Data.Entities.People;

public class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Interaction> Interactions { get; set; } = [];
    public ICollection<PersonFact> Facts { get; set; } = [];
    public ICollection<Promise> Promises { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/People/Organization.cs
namespace PAOS.Data.Entities.People;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string Notes { get; set; } = string.Empty;
}
```

```csharp
// PAOS.Data/Entities/People/Relationship.cs
namespace PAOS.Data.Entities.People;

public class Relationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string RelationshipType { get; set; } = string.Empty;
    public int Strength { get; set; }    // 1-10
    public string Notes { get; set; } = string.Empty;
}
```

```csharp
// PAOS.Data/Entities/People/Interaction.cs
namespace PAOS.Data.Entities.People;

public class Interaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Channel { get; set; } = string.Empty;   // email, call, meeting
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// PAOS.Data/Entities/People/PersonFact.cs
namespace PAOS.Data.Entities.People;

public class PersonFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Fact { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1.0f;
    public Guid? SourceId { get; set; }
}
```

```csharp
// PAOS.Data/Entities/People/Promise.cs
namespace PAOS.Data.Entities.People;

public class Promise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Write Project entities:**

```csharp
// PAOS.Data/Entities/Projects/Project.cs
namespace PAOS.Data.Entities.Projects;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProjectMember> Members { get; set; } = [];
    public ICollection<ProjectEvent> Events { get; set; } = [];
    public ICollection<ProjectBlocker> Blockers { get; set; } = [];
    public ICollection<ProjectFile> Files { get; set; } = [];
    public ICollection<ProjectStatusUpdate> StatusUpdates { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Projects/ProjectMember.cs
namespace PAOS.Data.Entities.Projects;

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid PersonId { get; set; }
    public string Role { get; set; } = string.Empty;
}
```

```csharp
// PAOS.Data/Entities/Projects/ProjectEvent.cs
namespace PAOS.Data.Entities.Projects;

public class ProjectEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
```

```csharp
// PAOS.Data/Entities/Projects/ProjectBlocker.cs
namespace PAOS.Data.Entities.Projects;

public class ProjectBlocker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// PAOS.Data/Entities/Projects/ProjectFile.cs
namespace PAOS.Data.Entities.Projects;

public class ProjectFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string FileKey { get; set; } = string.Empty;   // MinIO object key
    public string Filename { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// PAOS.Data/Entities/Projects/ProjectStatusUpdate.cs
namespace PAOS.Data.Entities.Projects;

public class ProjectStatusUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Status { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Write Commitment entities:**

```csharp
// PAOS.Data/Entities/Commitments/Commitment.cs
namespace PAOS.Data.Entities.Commitments;

public class Commitment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "open";
    public float Confidence { get; set; } = 1.0f;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CommitmentSource> Sources { get; set; } = [];
    public ICollection<CommitmentStatusHistory> StatusHistory { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Commitments/CommitmentSource.cs
namespace PAOS.Data.Entities.Commitments;

public class CommitmentSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommitmentId { get; set; }
    public Commitment Commitment { get; set; } = null!;
    public Guid SourceId { get; set; }
}
```

```csharp
// PAOS.Data/Entities/Commitments/CommitmentStatusHistory.cs
namespace PAOS.Data.Entities.Commitments;

public class CommitmentStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommitmentId { get; set; }
    public Commitment Commitment { get; set; } = null!;
    public string Status { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}
```

- [ ] **Write Episodic entities:**

```csharp
// PAOS.Data/Entities/Episodic/MemoryEvent.cs
namespace PAOS.Data.Entities.Episodic;

public class MemoryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;   // meeting, call, email, upload
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<EventParticipant> Participants { get; set; } = [];
    public ICollection<EventSource> Sources { get; set; } = [];
    public ICollection<EventSummary> Summaries { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Episodic/EventParticipant.cs
namespace PAOS.Data.Entities.Episodic;

public class EventParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MemoryEvent Event { get; set; } = null!;
    public Guid PersonId { get; set; }
    public string Role { get; set; } = string.Empty;
}
```

```csharp
// PAOS.Data/Entities/Episodic/EventSource.cs
namespace PAOS.Data.Entities.Episodic;

public class EventSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MemoryEvent Event { get; set; } = null!;
    public Guid SourceId { get; set; }
}
```

```csharp
// PAOS.Data/Entities/Episodic/EventSummary.cs
namespace PAOS.Data.Entities.Episodic;

public class EventSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MemoryEvent Event { get; set; } = null!;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Write Semantic (Factual) entities:**

```csharp
// PAOS.Data/Entities/Semantic/Fact.cs
namespace PAOS.Data.Entities.Semantic;

public class Fact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1.0f;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<FactSource> Sources { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Semantic/FactSource.cs
namespace PAOS.Data.Entities.Semantic;

public class FactSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FactId { get; set; }
    public Fact Fact { get; set; } = null!;
    public Guid SourceId { get; set; }
}
```

```csharp
// PAOS.Data/Entities/Semantic/FactConflict.cs
namespace PAOS.Data.Entities.Semantic;

public class FactConflict
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FactIdA { get; set; }
    public Guid FactIdB { get; set; }
    public string ConflictType { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// PAOS.Data/Entities/Semantic/FactConfidenceHistory.cs
namespace PAOS.Data.Entities.Semantic;

public class FactConfidenceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FactId { get; set; }
    public Fact Fact { get; set; } = null!;
    public float Confidence { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Write Procedural entities:**

```csharp
// PAOS.Data/Entities/Procedural/Procedure.cs
namespace PAOS.Data.Entities.Procedural;

public class Procedure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProcedureStep> Steps { get; set; } = [];
    public ICollection<ProcedureRun> Runs { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Procedural/ProcedureStep.cs
namespace PAOS.Data.Entities.Procedural;

public class ProcedureStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcedureId { get; set; }
    public Procedure Procedure { get; set; } = null!;
    public int StepOrder { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
}
```

```csharp
// PAOS.Data/Entities/Procedural/ProcedureRun.cs
namespace PAOS.Data.Entities.Procedural;

public class ProcedureRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcedureId { get; set; }
    public Procedure Procedure { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running";   // running, completed, failed
    public ICollection<ProcedureFeedback> Feedback { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Procedural/ProcedureFeedback.cs
namespace PAOS.Data.Entities.Procedural;

public class ProcedureFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcedureRunId { get; set; }
    public ProcedureRun ProcedureRun { get; set; } = null!;
    public int Rating { get; set; }    // 1-5
    public string Notes { get; set; } = string.Empty;
}
```

- [ ] **Write Decision entities:**

```csharp
// PAOS.Data/Entities/Decisions/Decision.cs
namespace PAOS.Data.Entities.Decisions;

public class Decision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime MadeAt { get; set; }
    public DateTime? RevisitAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DecisionOption> Options { get; set; } = [];
    public ICollection<DecisionAssumption> Assumptions { get; set; } = [];
    public ICollection<DecisionOutcome> Outcomes { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Decisions/DecisionOption.cs
namespace PAOS.Data.Entities.Decisions;

public class DecisionOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;
    public string OptionText { get; set; } = string.Empty;
    public bool WasChosen { get; set; }
}
```

```csharp
// PAOS.Data/Entities/Decisions/DecisionAssumption.cs
namespace PAOS.Data.Entities.Decisions;

public class DecisionAssumption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;
    public string AssumptionText { get; set; } = string.Empty;
    public bool StillValid { get; set; } = true;
}
```

```csharp
// PAOS.Data/Entities/Decisions/DecisionOutcome.cs
namespace PAOS.Data.Entities.Decisions;

public class DecisionOutcome
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;
    public string OutcomeDescription { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Write Search entities:**

```csharp
// PAOS.Data/Entities/Search/MemoryEmbedding.cs
using Pgvector;

namespace PAOS.Data.Entities.Search;

public class MemoryEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;  // source, person, project, commitment
    public Guid EntityId { get; set; }
    public Vector Embedding { get; set; } = null!;          // 1536 dims (text-embedding-3-small)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// PAOS.Data/Entities/Search/SearchLog.cs
using Pgvector;

namespace PAOS.Data.Entities.Search;

public class SearchLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Query { get; set; } = string.Empty;
    public Vector QueryEmbedding { get; set; } = null!;
    public string ResultsJson { get; set; } = "[]";
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RetrievalFeedback> Feedback { get; set; } = [];
}
```

```csharp
// PAOS.Data/Entities/Search/RetrievalFeedback.cs
namespace PAOS.Data.Entities.Search;

public class RetrievalFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SearchLogId { get; set; }
    public SearchLog SearchLog { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public bool WasHelpful { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Commit:**

```bash
git add PAOS.Data/Entities/
git commit -m "feat: define all 40 EF Core entities across all memory domains"
```

---

### Task 2.2 — Register All Entities in DbContext + Generate Migration

**Files:**
- Modify: `PAOS.Data/MemoryDbContext.cs`

- [ ] **Update MemoryDbContext.cs with all DbSets:**

```csharp
using Microsoft.EntityFrameworkCore;
using PAOS.Data.Entities.Sources;
using PAOS.Data.Entities.Identity;
using PAOS.Data.Entities.People;
using PAOS.Data.Entities.Projects;
using PAOS.Data.Entities.Commitments;
using PAOS.Data.Entities.Episodic;
using PAOS.Data.Entities.Semantic;
using PAOS.Data.Entities.Procedural;
using PAOS.Data.Entities.Decisions;
using PAOS.Data.Entities.Search;

namespace PAOS.Data;

public class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    // Sources
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<SourceChunk> SourceChunks => Set<SourceChunk>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Identity
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserGoal> UserGoals => Set<UserGoal>();
    public DbSet<UserValue> UserValues => Set<UserValue>();
    public DbSet<UserHabit> UserHabits => Set<UserHabit>();

    // People
    public DbSet<Person> People => Set<Person>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<PersonFact> PersonFacts => Set<PersonFact>();
    public DbSet<Promise> Promises => Set<Promise>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectEvent> ProjectEvents => Set<ProjectEvent>();
    public DbSet<ProjectBlocker> ProjectBlockers => Set<ProjectBlocker>();
    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();
    public DbSet<ProjectStatusUpdate> ProjectStatusUpdates => Set<ProjectStatusUpdate>();

    // Commitments
    public DbSet<Commitment> Commitments => Set<Commitment>();
    public DbSet<CommitmentSource> CommitmentSources => Set<CommitmentSource>();
    public DbSet<CommitmentStatusHistory> CommitmentStatusHistories => Set<CommitmentStatusHistory>();

    // Episodic
    public DbSet<MemoryEvent> MemoryEvents => Set<MemoryEvent>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<EventSource> EventSources => Set<EventSource>();
    public DbSet<EventSummary> EventSummaries => Set<EventSummary>();

    // Semantic
    public DbSet<Fact> Facts => Set<Fact>();
    public DbSet<FactSource> FactSources => Set<FactSource>();
    public DbSet<FactConflict> FactConflicts => Set<FactConflict>();
    public DbSet<FactConfidenceHistory> FactConfidenceHistories => Set<FactConfidenceHistory>();

    // Procedural
    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<ProcedureStep> ProcedureSteps => Set<ProcedureStep>();
    public DbSet<ProcedureRun> ProcedureRuns => Set<ProcedureRun>();
    public DbSet<ProcedureFeedback> ProcedureFeedback => Set<ProcedureFeedback>();

    // Decisions
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<DecisionOption> DecisionOptions => Set<DecisionOption>();
    public DbSet<DecisionAssumption> DecisionAssumptions => Set<DecisionAssumption>();
    public DbSet<DecisionOutcome> DecisionOutcomes => Set<DecisionOutcome>();

    // Search
    public DbSet<MemoryEmbedding> MemoryEmbeddings => Set<MemoryEmbedding>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<RetrievalFeedback> RetrievalFeedback => Set<RetrievalFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<MemoryEmbedding>()
            .Property(e => e.Embedding)
            .HasColumnType("vector(1536)");

        modelBuilder.Entity<SearchLog>()
            .Property(e => e.QueryEmbedding)
            .HasColumnType("vector(1536)");

        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Generate initial migration (run from PAOS.MemoryAPI directory, which has EF design tools):**

```bash
dotnet ef migrations add InitialSchema --project ../PAOS.Data --startup-project .
dotnet ef database update --project ../PAOS.Data --startup-project .
```

- [ ] **Commit:**

```bash
git add PAOS.Data/MemoryDbContext.cs PAOS.Data/Migrations/
git commit -m "feat: register all entities and generate initial DB migration"
```

---

### Task 2.3 — Source Ingestion Endpoint

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/SourceEndpoints.cs`

- [ ] **Write SourceEndpoints.cs:**

```csharp
using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class SourceEndpoints
{
    public static void MapSourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sources");

        group.MapPost("/", async (IngestSourceRequest req, MemoryDbContext db) =>
        {
            var source = new Source
            {
                Type = req.Type,
                RawContent = req.RawContent,
                ExtractionMethod = req.ExtractionMethod,
                Confidence = req.Confidence
            };

            var audit = new AuditLog
            {
                EntityType = "Source",
                EntityId = source.Id,
                Action = "created",
                ChangedBy = "api"
            };

            db.Sources.Add(source);
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync();

            return Results.Created($"/sources/{source.Id}", source);
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var source = await db.Sources
                .Include(s => s.Chunks)
                .FirstOrDefaultAsync(s => s.Id == id);

            return source is null ? Results.NotFound() : Results.Ok(source);
        });
    }
}

public record IngestSourceRequest(
    string Type,
    string RawContent,
    string ExtractionMethod = "manual",
    float Confidence = 1.0f);
```

- [ ] **Register in Program.cs — add after `app.MapHealthEndpoints()`:**

```csharp
app.MapSourceEndpoints();
```

- [ ] **Test locally (Postgres must be running):**

```bash
curl -X POST http://localhost:8000/sources \
  -H "Content-Type: application/json" \
  -d '{"type":"text","rawContent":"David committed to deliver the report by Friday","extractionMethod":"manual"}'
```

Expected: `201 Created` with source JSON including `id`.

- [ ] **Commit:**

```bash
git add PAOS.MemoryAPI/Endpoints/SourceEndpoints.cs PAOS.MemoryAPI/Program.cs
git commit -m "feat: source ingestion and retrieval endpoints with audit log"
```

---

### Task 2.4 — Phase 2 Verification

- [ ] **All 40+ tables exist in Postgres:** `\dt` in psql should list them all
- [ ] **`POST /sources` returns 201 with a GUID**
- [ ] **`GET /sources/{id}` returns the stored source**
- [ ] **`audit_logs` table has one row for the POST**

**Phase 2 complete when:** All 4 checks pass.

---

## Phase 3: Core Memory Types

**Goal:** Identity, People, Project, and Commitment memory fully implemented with CRUD endpoints and source linking.

**Milestone:** The spec's first production goal — "save a memory, link it to a person/project/commitment, embed it (queued), retrieve it, show its source."

**Each domain follows the same pattern:**
1. Request/response DTOs in `PAOS.Models`
2. Minimal API endpoints in `PAOS.MemoryAPI/Endpoints/`
3. Audit log written on every mutation

---

### Task 3.1 — Identity Memory Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/IdentityEndpoints.cs`

Implement endpoints:
- `POST /identity` — create user profile
- `GET /identity/{id}` — get profile with preferences, goals, values, habits
- `POST /identity/{id}/preferences` — add preference
- `POST /identity/{id}/goals` — add goal
- `PUT /identity/{id}/goals/{goalId}` — update goal status
- `DELETE /identity/{id}` — soft delete (set UpdatedAt, write audit log)

Each mutation writes an `AuditLog` row. Follow the same pattern as `SourceEndpoints.cs`.

- [ ] **Write IdentityEndpoints.cs with all 6 endpoints (follow SourceEndpoints pattern)**
- [ ] **Register in Program.cs: `app.MapIdentityEndpoints();`**
- [ ] **Test: POST → GET returns full profile with nested collections**
- [ ] **Commit: `feat: identity memory CRUD endpoints`**

---

### Task 3.2 — People Memory Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/PeopleEndpoints.cs`

Implement endpoints:
- `POST /people` — create person
- `GET /people/{id}` — get person with interactions, facts, promises
- `POST /people/{id}/interactions` — log interaction
- `POST /people/{id}/facts` — add fact (with optional sourceId)
- `POST /people/{id}/promises` — record promise
- `GET /people/{id}/promises?status=open` — list open promises
- `PUT /people/{id}/promises/{promiseId}` — update promise status

- [ ] **Write PeopleEndpoints.cs with all 7 endpoints**
- [ ] **Register in Program.cs: `app.MapPeopleEndpoints();`**
- [ ] **Test: create person → log interaction → create promise → list open promises**
- [ ] **Commit: `feat: people memory CRUD endpoints with promise tracking`**

---

### Task 3.3 — Project Memory Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/ProjectEndpoints.cs`

Implement endpoints:
- `POST /projects` — create project
- `GET /projects/{id}` — get project with members, blockers, status updates
- `POST /projects/{id}/members` — add member (links to Person by personId)
- `POST /projects/{id}/blockers` — add blocker
- `PUT /projects/{id}/blockers/{blockerId}/resolve` — mark blocker resolved
- `POST /projects/{id}/status` — post status update
- `POST /projects/{id}/files` — register file reference (MinIO key)

- [ ] **Write ProjectEndpoints.cs with all 7 endpoints**
- [ ] **Register in Program.cs: `app.MapProjectEndpoints();`**
- [ ] **Test: create project → add blocker → resolve it → get project shows resolvedAt**
- [ ] **Commit: `feat: project memory endpoints with blocker tracking`**

---

### Task 3.4 — Commitment Memory Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/CommitmentEndpoints.cs`

Implement endpoints:
- `POST /commitments` — create commitment (accepts optional sourceId array)
- `GET /commitments` — list all, filter by `?status=open`
- `GET /commitments/{id}` — get with sources and status history
- `PUT /commitments/{id}/status` — update status (appends to history)

- [ ] **Write CommitmentEndpoints.cs with all 4 endpoints**
- [ ] **Register in Program.cs: `app.MapCommitmentEndpoints();`**
- [ ] **Test: POST commitment with sourceId → GET shows source linked → update status → history has 2 rows**
- [ ] **Commit: `feat: commitment memory endpoints with source linking and status history`**

---

### Task 3.5 — Phase 3 Verification

- [ ] **End-to-end flow:**
  1. `POST /sources` with raw text "I promised Alice the budget by Monday"
  2. `POST /people` for Alice
  3. `POST /commitments` with the sourceId from step 1, linked to Alice's personId
  4. `GET /commitments/{id}` shows commitment + source + open status
  5. `PUT /commitments/{id}/status` body `{"status":"completed","notes":"Delivered"}` → 200
  6. `GET /commitments/{id}` shows 2 status history rows

**Phase 3 complete when:** All 6 steps pass in sequence.

---

## Phase 4: Search

**Goal:** Natural-language queries return semantically relevant memories with linked evidence.

**Milestone:** `POST /search` with `{"query":"What did I promise Alice?"}` returns the commitment from Phase 3 with source.

---

### Task 4.1 — Embedding Pipeline (Worker)

**Files:**
- Modify: `PAOS.MemoryWorker/PAOS.MemoryWorker.csproj` — add OpenAI package
- Modify: `PAOS.MemoryWorker/Workers/EmbeddingWorker.cs`

- [ ] **Add OpenAI package to PAOS.MemoryWorker.csproj:**

```xml
<PackageReference Include="OpenAI" Version="2.*" />
<PackageReference Include="StackExchange.Redis" Version="2.*" />
```

- [ ] **Update EmbeddingWorker.cs to poll Redis queue `embed_queue` and write vectors:**

```csharp
using Microsoft.EntityFrameworkCore;
using OpenAI.Embeddings;
using PAOS.Data;
using PAOS.Data.Entities.Search;
using Pgvector;
using StackExchange.Redis;

namespace PAOS.MemoryWorker.Workers;

public class EmbeddingWorker(
    ILogger<EmbeddingWorker> logger,
    IConfiguration config,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(config.GetConnectionString("Redis")!);
        var db = redis.GetDatabase();
        var embeddingClient = new EmbeddingClient("text-embedding-3-small",
            config["OpenAI:ApiKey"]!);

        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await db.ListRightPopAsync("embed_queue");
            if (job == RedisValue.Null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            // job format: "EntityType:EntityId:TextToEmbed"
            var parts = ((string)job!).Split(':', 3);
            if (parts.Length != 3) continue;

            var (entityType, entityId, text) = (parts[0], Guid.Parse(parts[1]), parts[2]);

            try
            {
                var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: stoppingToken);
                var vector = new Vector(result.Value.ToFloats().ToArray());

                using var scope = scopeFactory.CreateScope();
                var memDb = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
                memDb.MemoryEmbeddings.Add(new MemoryEmbedding
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    Embedding = vector
                });
                await memDb.SaveChangesAsync(stoppingToken);

                logger.LogInformation("Embedded {Type} {Id}", entityType, entityId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to embed {Type} {Id}", entityType, entityId);
            }
        }
    }
}
```

- [ ] **Queue embedding from SourceEndpoints after save — inject IConnectionMultiplexer and push to `embed_queue`:**

In `SourceEndpoints.cs`, after `db.SaveChangesAsync()`:
```csharp
var redisDb = redis.GetDatabase();
await redisDb.ListLeftPushAsync("embed_queue",
    $"Source:{source.Id}:{source.RawContent}");
```

- [ ] **Commit:**

```bash
git add PAOS.MemoryWorker/ PAOS.MemoryAPI/Endpoints/SourceEndpoints.cs
git commit -m "feat: embedding pipeline via Redis queue and pgvector storage"
```

---

### Task 4.2 — Semantic Search Endpoint

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/SearchEndpoints.cs`

- [ ] **Write SearchEndpoints.cs:**

```csharp
using Microsoft.EntityFrameworkCore;
using OpenAI.Embeddings;
using PAOS.Data;
using PAOS.Data.Entities.Search;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace PAOS.MemoryAPI.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        app.MapPost("/search", async (SearchRequest req, MemoryDbContext db, IConfiguration config) =>
        {
            var embeddingClient = new EmbeddingClient("text-embedding-3-small", config["OpenAI:ApiKey"]!);
            var result = await embeddingClient.GenerateEmbeddingAsync(req.Query);
            var queryVector = new Vector(result.Value.ToFloats().ToArray());

            var hits = await db.MemoryEmbeddings
                .OrderBy(e => e.Embedding.CosineDistance(queryVector))
                .Take(req.Limit)
                .Select(e => new { e.EntityType, e.EntityId, e.CreatedAt })
                .ToListAsync();

            var log = new SearchLog
            {
                Query = req.Query,
                QueryEmbedding = queryVector,
                ResultsJson = System.Text.Json.JsonSerializer.Serialize(hits)
            };
            db.SearchLogs.Add(log);
            await db.SaveChangesAsync();

            return Results.Ok(new { searchLogId = log.Id, results = hits });
        });

        app.MapPost("/search/keyword", async (KeywordSearchRequest req, MemoryDbContext db) =>
        {
            var sources = await db.Sources
                .Where(s => EF.Functions.ILike(s.RawContent, $"%{req.Query}%"))
                .Take(req.Limit)
                .ToListAsync();

            return Results.Ok(sources);
        });
    }
}

public record SearchRequest(string Query, int Limit = 10);
public record KeywordSearchRequest(string Query, int Limit = 10);
```

- [ ] **Register in Program.cs: `app.MapSearchEndpoints();`**
- [ ] **Create pgvector index for cosine similarity (add to a new migration):**

```bash
dotnet ef migrations add AddVectorIndex --project ../PAOS.Data --startup-project .
```

In the generated migration `Up()` method, add:
```csharp
migrationBuilder.Sql(
    "CREATE INDEX IF NOT EXISTS ix_memory_embeddings_vector ON memory_embeddings USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100)");
```

- [ ] **Commit:**

```bash
git add PAOS.MemoryAPI/Endpoints/SearchEndpoints.cs PAOS.Data/Migrations/
git commit -m "feat: semantic search and keyword search endpoints with pgvector cosine similarity"
```

---

### Task 4.3 — Phase 4 Verification

- [ ] **Ingest a source, wait 3 seconds for worker to embed it**
- [ ] **`POST /search` with related query — response includes the source EntityId**
- [ ] **`POST /search/keyword` with a word from the source — returns the source**
- [ ] **`search_logs` table has a row with the query vector**

**Phase 4 complete when:** Semantic search returns the right memory without knowing its ID.

---

## Phase 5: Remaining Memory Types

**Goal:** All memory domains operational — Episodic, Semantic (facts), Procedural, Decision.

**Milestone:** Full memory taxonomy complete. All 10 memory types from the spec are queryable.

---

### Task 5.1 — Episodic Memory Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/EpisodicEndpoints.cs`

Implement endpoints:
- `POST /events` — create event (meeting, call, email, upload)
- `GET /events/{id}` — get with participants, sources, summaries
- `POST /events/{id}/participants` — add participant (links to Person)
- `POST /events/{id}/sources` — link a Source to this event
- `GET /events?person={id}` — timeline for a person
- `GET /events?project={id}` — timeline for a project (via participants who are project members)

- [ ] **Write EpisodicEndpoints.cs with all 6 endpoints**
- [ ] **Register in Program.cs: `app.MapEpisodicEndpoints();`**
- [ ] **Queue embedding on event creation (same Redis pattern as sources)**
- [ ] **Commit: `feat: episodic memory endpoints with timeline queries`**

---

### Task 5.2 — Semantic (Facts) Memory Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/FactEndpoints.cs`

Implement endpoints:
- `POST /facts` — create fact (subject/predicate/object + optional sourceId)
- `GET /facts/{id}` — get fact with sources and confidence history
- `GET /facts?subject={subject}` — look up facts about a subject
- `POST /facts/{id}/confidence` — update confidence (appends to history)
- `GET /facts/conflicts` — list detected conflicts

- [ ] **Write FactEndpoints.cs with all 5 endpoints**
- [ ] **Register in Program.cs: `app.MapFactEndpoints();`**
- [ ] **Commit: `feat: semantic fact memory endpoints with confidence tracking`**

---

### Task 5.3 — Procedural Memory Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/ProceduralEndpoints.cs`

Implement endpoints:
- `POST /procedures` — define a procedure with steps
- `GET /procedures/{id}` — get procedure with steps and run history
- `POST /procedures/{id}/runs` — start a run
- `PUT /procedures/{id}/runs/{runId}` — complete/fail a run
- `POST /procedures/{id}/runs/{runId}/feedback` — add feedback

- [ ] **Write ProceduralEndpoints.cs with all 5 endpoints**
- [ ] **Register in Program.cs: `app.MapProceduralEndpoints();`**
- [ ] **Commit: `feat: procedural memory endpoints with run history and feedback`**

---

### Task 5.4 — Decision Journal Endpoints

**Files:**
- Create: `PAOS.MemoryAPI/Endpoints/DecisionEndpoints.cs`

Implement endpoints:
- `POST /decisions` — record decision with options and assumptions
- `GET /decisions/{id}` — get with options, assumptions, outcomes
- `POST /decisions/{id}/outcomes` — record an outcome
- `PUT /decisions/{id}/assumptions/{assumptionId}` — mark assumption invalid
- `GET /decisions?revisitBefore={date}` — decisions due for review

- [ ] **Write DecisionEndpoints.cs with all 5 endpoints**
- [ ] **Register in Program.cs: `app.MapDecisionEndpoints();`**
- [ ] **Commit: `feat: decision journal endpoints with assumption tracking and review scheduling`**

---

### Task 5.5 — Phase 5 Verification

- [ ] **Create an event, link a person and a source, retrieve with full timeline**
- [ ] **Create a fact, update its confidence, check history has 2 rows**
- [ ] **Create a procedure, start and complete a run, add feedback**
- [ ] **Create a decision with 2 options and 1 assumption, record outcome, retrieve full decision**

**Phase 5 complete when:** All 4 checks pass.

---

## Final System Verification

At this point the system satisfies the stated first production goal:

| Goal | Implemented In |
|------|---------------|
| Ingest memory | `POST /sources` (Phase 2) |
| Classify memory | Worker classification (Phase 4 worker) |
| Link to entities | CommitmentSource, PersonFact, EventSource (Phase 3) |
| Embed memory | EmbeddingWorker + pgvector (Phase 4) |
| Retrieve semantically | `POST /search` cosine distance (Phase 4) |
| Show evidence | Source included in every GET response (Phase 2) |
| Audit history | AuditLog written on every mutation (Phase 2) |

Run the end-to-end smoke test:
```bash
# 1. Ingest raw text
SOURCE=$(curl -s -X POST http://localhost:8000/sources \
  -H "Content-Type: application/json" \
  -d '{"type":"text","rawContent":"I committed to deliver the Q2 report to Alice by Friday"}')
SOURCE_ID=$(echo $SOURCE | jq -r '.id')

# 2. Create person
ALICE=$(curl -s -X POST http://localhost:8000/people \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice"}')
ALICE_ID=$(echo $ALICE | jq -r '.id')

# 3. Create commitment linked to source and person
COMMITMENT=$(curl -s -X POST http://localhost:8000/commitments \
  -H "Content-Type: application/json" \
  -d "{\"description\":\"Deliver Q2 report to Alice\",\"ownerId\":\"$ALICE_ID\",\"sourceIds\":[\"$SOURCE_ID\"]}")

# 4. Wait for embedding
sleep 5

# 5. Semantic search
curl -s -X POST http://localhost:8000/search \
  -H "Content-Type: application/json" \
  -d '{"query":"What did I commit to deliver?"}'
# Expected: result includes the commitment's source EntityId
```
