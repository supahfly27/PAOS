using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Semantic;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class SemanticEndpoints
{
    public static void MapSemanticEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/facts");

        group.MapPost("/", async (CreateFactRequest req, MemoryDbContext db) =>
        {
            var fact = new Fact
            {
                Subject = req.Subject,
                Predicate = req.Predicate,
                Object = req.Object,
                Confidence = req.Confidence
            };

            db.Facts.Add(fact);

            if (req.SourceIds is { Length: > 0 })
            {
                foreach (var sourceId in req.SourceIds)
                    db.FactSources.Add(new FactSource { FactId = fact.Id, SourceId = sourceId });
            }

            db.FactConfidenceHistories.Add(new FactConfidenceHistory
            {
                FactId = fact.Id,
                Confidence = req.Confidence
            });

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Fact",
                EntityId = fact.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/facts/{fact.Id}", new { id = fact.Id });
        });

        group.MapGet("/", async (string? subject, MemoryDbContext db) =>
        {
            var query = db.Facts.Include(f => f.Sources).AsQueryable();

            if (!string.IsNullOrEmpty(subject))
                query = query.Where(f => f.Subject == subject);

            var facts = await query.ToListAsync();
            return Results.Ok(facts);
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var fact = await db.Facts
                .Include(f => f.Sources)
                .FirstOrDefaultAsync(f => f.Id == id);

            return fact is null ? Results.NotFound() : Results.Ok(fact);
        });

        group.MapPut("/{id:guid}/confidence", async (Guid id, UpdateConfidenceRequest req, MemoryDbContext db) =>
        {
            var fact = await db.Facts.FindAsync(id);
            if (fact is null) return Results.NotFound();

            fact.Confidence = req.Confidence;

            db.FactConfidenceHistories.Add(new FactConfidenceHistory
            {
                FactId = id,
                Confidence = req.Confidence
            });

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Fact",
                EntityId = id,
                Action = "updated",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { id = fact.Id, confidence = fact.Confidence });
        });
    }
}

public record CreateFactRequest(
    string Subject,
    string Predicate,
    string Object,
    float Confidence = 1.0f,
    Guid[]? SourceIds = null);

public record UpdateConfidenceRequest(float Confidence);
