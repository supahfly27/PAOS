using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Episodic;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class EpisodicEndpoints
{
    public static void MapEpisodicEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/events");

        group.MapPost("/", async (CreateEventRequest req, MemoryDbContext db) =>
        {
            var ev = new MemoryEvent
            {
                Type = req.Type,
                Summary = req.Summary,
                OccurredAt = req.OccurredAt ?? DateTime.UtcNow
            };

            db.MemoryEvents.Add(ev);

            if (req.SourceIds is { Length: > 0 })
            {
                foreach (var sourceId in req.SourceIds)
                    db.EventSources.Add(new EventSource { EventId = ev.Id, SourceId = sourceId });
            }

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "MemoryEvent",
                EntityId = ev.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/events/{ev.Id}", new { id = ev.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var ev = await db.MemoryEvents
                .Include(e => e.Participants)
                .Include(e => e.Sources)
                .Include(e => e.Summaries)
                .FirstOrDefaultAsync(e => e.Id == id);

            return ev is null ? Results.NotFound() : Results.Ok(ev);
        });

        group.MapPost("/{id:guid}/participants", async (Guid id, AddParticipantRequest req, MemoryDbContext db) =>
        {
            if (!await db.MemoryEvents.AnyAsync(e => e.Id == id))
                return Results.NotFound();

            var participant = new EventParticipant
            {
                EventId = id,
                PersonId = req.PersonId,
                Role = req.Role ?? string.Empty
            };

            db.EventParticipants.Add(participant);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "EventParticipant",
                EntityId = participant.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/events/{id}/participants/{participant.Id}", new { id = participant.Id });
        });

        group.MapPost("/{id:guid}/sources", async (Guid id, AddEventSourceRequest req, MemoryDbContext db) =>
        {
            if (!await db.MemoryEvents.AnyAsync(e => e.Id == id))
                return Results.NotFound();

            var eventSource = new EventSource { EventId = id, SourceId = req.SourceId };
            db.EventSources.Add(eventSource);

            await db.SaveChangesAsync();
            return Results.Created($"/events/{id}/sources/{eventSource.Id}", new { id = eventSource.Id });
        });

        group.MapPost("/{id:guid}/summaries", async (Guid id, AddSummaryRequest req, MemoryDbContext db) =>
        {
            if (!await db.MemoryEvents.AnyAsync(e => e.Id == id))
                return Results.NotFound();

            var summary = new EventSummary
            {
                EventId = id,
                Summary = req.Summary
            };

            db.EventSummaries.Add(summary);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "EventSummary",
                EntityId = summary.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/events/{id}/summaries/{summary.Id}", new { id = summary.Id });
        });
    }
}

public record CreateEventRequest(
    string Type,
    string Summary,
    DateTime? OccurredAt = null,
    Guid[]? SourceIds = null);

public record AddParticipantRequest(Guid PersonId, string? Role = null);

public record AddEventSourceRequest(Guid SourceId);

public record AddSummaryRequest(string Summary);
