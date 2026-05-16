# PAOS.Data

Shared class library: EF Core DbContext, all entities, all migrations. Referenced by MemoryAPI, MemoryWorker, and Tests.

## Key Files

| File | Purpose |
|------|---------|
| `MemoryDbContext.cs` | Single DbContext with all DbSets; configures `HasPostgresExtension("vector")` |
| `MemoryDbContextFactory.cs` | Design-time factory for `dotnet ef` commands; reads `ConnectionStrings__Postgres` env var, falls back to `localhost:5432` |
| `Migrations/20260516070246_InitialSchema.cs` | All 40+ tables; vector columns added via raw SQL (`ALTER TABLE ... ADD COLUMN "Embedding" vector(1536)`) because EF maps them as `[NotMapped]` |
| `Migrations/20260516073815_AddVectorIndex.cs` | Adds `ivfflat` index on `MemoryEmbeddings.Embedding` via raw SQL |

## Running Migrations
```bash
# From Solution1/ root:
dotnet ef migrations add <Name> --project PAOS.Data --startup-project PAOS.Data
dotnet ef database update         --project PAOS.Data --startup-project PAOS.Data
```

## Entity Map

### Sources (`Entities/Sources/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `Source` | `Sources` | `Id, Type, RawContent, ExtractionMethod, Confidence, CreatedAt` → nav: `Chunks[]` |
| `SourceChunk` | `SourceChunks` | `Id, SourceId, ChunkText, ChunkIndex` |
| `AuditLog` | `AuditLogs` | `Id, EntityType, EntityId, Action, ChangedBy, ChangedAt, DiffJson` |

### Identity (`Entities/Identity/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `UserProfile` | `UserProfiles` | `Id, DisplayName, Timezone, CommunicationStyle, CreatedAt, UpdatedAt` → nav: `Preferences[], Goals[], Values[], Habits[]` |
| `UserPreference` | `UserPreferences` | `Id, UserProfileId, Key, Value, UpdatedAt` |
| `UserGoal` | `UserGoals` | `Id, UserProfileId, Goal, Priority, Status` |
| `UserValue` | `UserValues` | `Id, UserProfileId, ValueName, Description` |
| `UserHabit` | `UserHabits` | `Id, UserProfileId, Habit, Frequency` |

### People (`Entities/People/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `Person` | `People` | `Id, Name, Email?, Phone?, Notes` → nav: `Interactions[], Facts[], Promises[]` |
| `Organization` | `Organizations` | `Id, Name, Domain?, Notes` |
| `Relationship` | `Relationships` | `Id, PersonId, RelationshipType, Strength(1-10), Notes` |
| `Interaction` | `Interactions` | `Id, PersonId, Channel, Summary, OccurredAt` |
| `PersonFact` | `PersonFacts` | `Id, PersonId, Fact, Confidence, SourceId?` |
| `Promise` | `Promises` | `Id, PersonId, Description, DueDate?, Status(open/...)` |

### Projects (`Entities/Projects/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `Project` | `Projects` | `Id, Name, Description, Status` → nav: `Members[], Events[], Blockers[], Files[], StatusUpdates[]` |
| `ProjectMember` | `ProjectMembers` | `Id, ProjectId, PersonId, Role` |
| `ProjectEvent` | `ProjectEvents` | `Id, ProjectId, EventType, Description, OccurredAt` |
| `ProjectBlocker` | `ProjectBlockers` | `Id, ProjectId, Description, ResolvedAt?, CreatedAt` |
| `ProjectFile` | `ProjectFiles` | `Id, ProjectId, FileKey(MinIO), Filename, UploadedAt` |
| `ProjectStatusUpdate` | `ProjectStatusUpdates` | `Id, ProjectId, Status, Summary, CreatedAt` |

### Commitments (`Entities/Commitments/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `Commitment` | `Commitments` | `Id, Description, OwnerId?, DueDate?, Status, Confidence` → nav: `Sources[], StatusHistory[]` |
| `CommitmentSource` | `CommitmentSources` | `Id, CommitmentId, SourceId` |
| `CommitmentStatusHistory` | `CommitmentStatusHistories` | `Id, CommitmentId, Status, ChangedAt, Notes` |

### Episodic (`Entities/Episodic/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `MemoryEvent` | `MemoryEvents` | `Id, Type, Summary, OccurredAt` → nav: `Participants[], Sources[], Summaries[]` |
| `EventParticipant` | `EventParticipants` | `Id, EventId, PersonId, Role` |
| `EventSource` | `EventSources` | `Id, EventId, SourceId` |
| `EventSummary` | `EventSummaries` | `Id, EventId, Summary, CreatedAt` |

### Semantic (`Entities/Semantic/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `Fact` | `Facts` | `Id, Subject, Predicate, Object, Confidence` → nav: `Sources[]` |
| `FactSource` | `FactSources` | `Id, FactId, SourceId` |
| `FactConflict` | `FactConflicts` | `Id, FactIdA, FactIdB, ConflictType, DetectedAt` |
| `FactConfidenceHistory` | `FactConfidenceHistories` | `Id, FactId, Confidence, ChangedAt` |

### Procedural (`Entities/Procedural/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `Procedure` | `Procedures` | `Id, Name, Description` → nav: `Steps[], Runs[]` |
| `ProcedureStep` | `ProcedureSteps` | `Id, ProcedureId, StepOrder, Action, ParametersJson("{}")` |
| `ProcedureRun` | `ProcedureRuns` | `Id, ProcedureId, StartedAt, CompletedAt?, Status(running/completed/failed)` → nav: `Feedback[]` |
| `ProcedureFeedback` | `ProcedureFeedback` | `Id, ProcedureRunId, Rating(1-5), Notes` |

### Decisions (`Entities/Decisions/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `Decision` | `Decisions` | `Id, Title, Description, MadeAt, RevisitAt?` → nav: `Options[], Assumptions[], Outcomes[]` |
| `DecisionOption` | `DecisionOptions` | `Id, DecisionId, OptionText, WasChosen` |
| `DecisionAssumption` | `DecisionAssumptions` | `Id, DecisionId, AssumptionText, StillValid(default true)` |
| `DecisionOutcome` | `DecisionOutcomes` | `Id, DecisionId, OutcomeDescription, RecordedAt` |

### Search (`Entities/Search/`)
| Entity | Table | Key Fields |
|--------|-------|-----------|
| `MemoryEmbedding` | `MemoryEmbeddings` | `Id, EntityType, EntityId, CreatedAt` — `Embedding vector(1536)` column exists in DB but is `[NotMapped]` in EF; insert/query via raw Npgsql |
| `SearchLog` | `SearchLogs` | `Id, Query, ResultsJson, SearchedAt` — `QueryEmbedding vector(1536)` also `[NotMapped]` |
| `RetrievalFeedback` | `RetrievalFeedback` | `Id, SearchLogId, EntityType, EntityId, WasHelpful` |

## Vector Column Warning
`MemoryEmbedding.Embedding` and `SearchLog.QueryEmbedding` are `[NotMapped]`. The columns exist in Postgres (added via raw SQL in InitialSchema). Do NOT remove `[NotMapped]` — EF will try to re-add the columns in a new migration and fail. Use raw Npgsql commands for all vector reads/writes.
