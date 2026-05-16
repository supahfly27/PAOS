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
