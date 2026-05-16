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
        base.OnModelCreating(modelBuilder);
    }
}
