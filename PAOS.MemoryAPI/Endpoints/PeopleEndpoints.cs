using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.People;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class PeopleEndpoints
{
    public static void MapPeopleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/people");

        group.MapPost("/", async (CreatePersonRequest req, MemoryDbContext db) =>
        {
            var person = new Person
            {
                Name = req.Name,
                Email = req.Email,
                Phone = req.Phone,
                Notes = req.Notes ?? string.Empty
            };

            db.People.Add(person);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Person",
                EntityId = person.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/people/{person.Id}", new { id = person.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var person = await db.People
                .Include(p => p.Interactions)
                .Include(p => p.Facts)
                .Include(p => p.Promises)
                .FirstOrDefaultAsync(p => p.Id == id);

            return person is null ? Results.NotFound() : Results.Ok(person);
        });

        group.MapPost("/{id:guid}/interactions", async (Guid id, LogInteractionRequest req, MemoryDbContext db) =>
        {
            var person = await db.People.FindAsync(id);
            if (person is null) return Results.NotFound();

            var interaction = new Interaction
            {
                PersonId = id,
                Channel = req.Channel,
                Summary = req.Summary,
                OccurredAt = req.OccurredAt ?? DateTime.UtcNow
            };

            db.Interactions.Add(interaction);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Interaction",
                EntityId = interaction.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/people/{id}/interactions/{interaction.Id}", new { id = interaction.Id });
        });

        group.MapPost("/{id:guid}/facts", async (Guid id, AddPersonFactRequest req, MemoryDbContext db) =>
        {
            var person = await db.People.FindAsync(id);
            if (person is null) return Results.NotFound();

            var fact = new PersonFact
            {
                PersonId = id,
                Fact = req.Fact,
                Confidence = req.Confidence,
                SourceId = req.SourceId
            };

            db.PersonFacts.Add(fact);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "PersonFact",
                EntityId = fact.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/people/{id}/facts/{fact.Id}", new { id = fact.Id });
        });

        group.MapPost("/{id:guid}/promises", async (Guid id, RecordPromiseRequest req, MemoryDbContext db) =>
        {
            var person = await db.People.FindAsync(id);
            if (person is null) return Results.NotFound();

            var promise = new Promise
            {
                PersonId = id,
                Description = req.Description,
                DueDate = req.DueDate
            };

            db.Promises.Add(promise);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Promise",
                EntityId = promise.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/people/{id}/promises/{promise.Id}", new { id = promise.Id });
        });

        group.MapGet("/{id:guid}/promises", async (Guid id, string? status, MemoryDbContext db) =>
        {
            var person = await db.People.FindAsync(id);
            if (person is null) return Results.NotFound();

            var query = db.Promises.Where(p => p.PersonId == id);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);

            var promises = await query.ToListAsync();
            return Results.Ok(promises);
        });

        group.MapPut("/{id:guid}/promises/{promiseId:guid}", async (Guid id, Guid promiseId, UpdatePromiseStatusRequest req, MemoryDbContext db) =>
        {
            var promise = await db.Promises.FirstOrDefaultAsync(p => p.Id == promiseId && p.PersonId == id);
            if (promise is null) return Results.NotFound();

            promise.Status = req.Status;

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Promise",
                EntityId = promise.Id,
                Action = "updated",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Ok(new { id = promise.Id, status = promise.Status });
        });
    }
}

public record CreatePersonRequest(string Name, string? Email = null, string? Phone = null, string? Notes = null);
public record LogInteractionRequest(string Channel, string Summary, DateTime? OccurredAt = null);
public record AddPersonFactRequest(string Fact, float Confidence = 1.0f, Guid? SourceId = null);
public record RecordPromiseRequest(string Description, DateTime? DueDate = null);
public record UpdatePromiseStatusRequest(string Status);
