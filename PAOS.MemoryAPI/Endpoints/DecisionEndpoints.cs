using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Decisions;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class DecisionEndpoints
{
    public static void MapDecisionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/decisions");

        group.MapPost("/", async (CreateDecisionRequest req, MemoryDbContext db) =>
        {
            var decision = new Decision
            {
                Title = req.Title,
                Description = req.Description ?? string.Empty,
                MadeAt = req.MadeAt ?? DateTime.UtcNow,
                RevisitAt = req.RevisitAt
            };

            db.Decisions.Add(decision);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Decision",
                EntityId = decision.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/decisions/{decision.Id}", new { id = decision.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var decision = await db.Decisions
                .Include(d => d.Options)
                .Include(d => d.Assumptions)
                .Include(d => d.Outcomes)
                .FirstOrDefaultAsync(d => d.Id == id);

            return decision is null ? Results.NotFound() : Results.Ok(decision);
        });

        group.MapPost("/{id:guid}/options", async (Guid id, AddOptionRequest req, MemoryDbContext db) =>
        {
            if (!await db.Decisions.AnyAsync(d => d.Id == id))
                return Results.NotFound();

            var option = new DecisionOption
            {
                DecisionId = id,
                OptionText = req.OptionText,
                WasChosen = req.WasChosen
            };

            db.DecisionOptions.Add(option);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "DecisionOption",
                EntityId = option.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/decisions/{id}/options/{option.Id}", new { id = option.Id });
        });

        group.MapPost("/{id:guid}/assumptions", async (Guid id, AddAssumptionRequest req, MemoryDbContext db) =>
        {
            if (!await db.Decisions.AnyAsync(d => d.Id == id))
                return Results.NotFound();

            var assumption = new DecisionAssumption
            {
                DecisionId = id,
                AssumptionText = req.AssumptionText,
                StillValid = true
            };

            db.DecisionAssumptions.Add(assumption);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "DecisionAssumption",
                EntityId = assumption.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/decisions/{id}/assumptions/{assumption.Id}", new { id = assumption.Id });
        });

        group.MapPut("/{id:guid}/assumptions/{assumptionId:guid}/invalidate", async (Guid id, Guid assumptionId, MemoryDbContext db) =>
        {
            var assumption = await db.DecisionAssumptions
                .FirstOrDefaultAsync(a => a.Id == assumptionId && a.DecisionId == id);
            if (assumption is null) return Results.NotFound();

            assumption.StillValid = false;

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "DecisionAssumption",
                EntityId = assumptionId,
                Action = "invalidated",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { id = assumption.Id, stillValid = assumption.StillValid });
        });

        group.MapPost("/{id:guid}/outcomes", async (Guid id, AddOutcomeRequest req, MemoryDbContext db) =>
        {
            if (!await db.Decisions.AnyAsync(d => d.Id == id))
                return Results.NotFound();

            var outcome = new DecisionOutcome
            {
                DecisionId = id,
                OutcomeDescription = req.OutcomeDescription
            };

            db.DecisionOutcomes.Add(outcome);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "DecisionOutcome",
                EntityId = outcome.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/decisions/{id}/outcomes/{outcome.Id}", new { id = outcome.Id });
        });
    }
}

public record CreateDecisionRequest(
    string Title,
    string? Description = null,
    DateTime? MadeAt = null,
    DateTime? RevisitAt = null);

public record AddOptionRequest(string OptionText, bool WasChosen = false);

public record AddAssumptionRequest(string AssumptionText);

public record AddOutcomeRequest(string OutcomeDescription);
