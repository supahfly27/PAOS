using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Procedural;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class ProceduralEndpoints
{
    public static void MapProceduralEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/procedures");

        group.MapPost("/", async (CreateProcedureRequest req, MemoryDbContext db) =>
        {
            var procedure = new Procedure
            {
                Name = req.Name,
                Description = req.Description ?? string.Empty
            };

            db.Procedures.Add(procedure);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Procedure",
                EntityId = procedure.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/procedures/{procedure.Id}", new { id = procedure.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var procedure = await db.Procedures
                .Include(p => p.Steps.OrderBy(s => s.StepOrder))
                .Include(p => p.Runs)
                .FirstOrDefaultAsync(p => p.Id == id);

            return procedure is null ? Results.NotFound() : Results.Ok(procedure);
        });

        group.MapPost("/{id:guid}/steps", async (Guid id, AddStepRequest req, MemoryDbContext db) =>
        {
            if (!await db.Procedures.AnyAsync(p => p.Id == id))
                return Results.NotFound();

            var step = new ProcedureStep
            {
                ProcedureId = id,
                StepOrder = req.StepOrder,
                Action = req.Action,
                ParametersJson = req.ParametersJson ?? "{}"
            };

            db.ProcedureSteps.Add(step);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProcedureStep",
                EntityId = step.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/procedures/{id}/steps/{step.Id}", new { id = step.Id });
        });

        group.MapPost("/{id:guid}/runs", async (Guid id, MemoryDbContext db) =>
        {
            if (!await db.Procedures.AnyAsync(p => p.Id == id))
                return Results.NotFound();

            var run = new ProcedureRun
            {
                ProcedureId = id,
                Status = "running"
            };

            db.ProcedureRuns.Add(run);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProcedureRun",
                EntityId = run.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/procedures/{id}/runs/{run.Id}", new { id = run.Id });
        });

        group.MapPut("/{id:guid}/runs/{runId:guid}/complete", async (Guid id, Guid runId, MemoryDbContext db) =>
        {
            var run = await db.ProcedureRuns.FirstOrDefaultAsync(r => r.Id == runId && r.ProcedureId == id);
            if (run is null) return Results.NotFound();

            run.CompletedAt = DateTime.UtcNow;
            run.Status = "completed";

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProcedureRun",
                EntityId = runId,
                Action = "completed",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { id = run.Id, status = run.Status, completedAt = run.CompletedAt });
        });

        group.MapPost("/{id:guid}/runs/{runId:guid}/feedback", async (Guid id, Guid runId, AddFeedbackRequest req, MemoryDbContext db) =>
        {
            if (!await db.ProcedureRuns.AnyAsync(r => r.Id == runId && r.ProcedureId == id))
                return Results.NotFound();

            var feedback = new ProcedureFeedback
            {
                ProcedureRunId = runId,
                Rating = req.Rating,
                Notes = req.Notes ?? string.Empty
            };

            db.ProcedureFeedback.Add(feedback);

            await db.SaveChangesAsync();
            return Results.Created($"/procedures/{id}/runs/{runId}/feedback/{feedback.Id}", new { id = feedback.Id });
        });
    }
}

public record CreateProcedureRequest(string Name, string? Description = null);

public record AddStepRequest(int StepOrder, string Action, string? ParametersJson = null);

public record AddFeedbackRequest(int Rating, string? Notes = null);
