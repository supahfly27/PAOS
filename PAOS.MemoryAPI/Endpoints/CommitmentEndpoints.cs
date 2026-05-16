using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Commitments;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class CommitmentEndpoints
{
    public static void MapCommitmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/commitments");

        group.MapPost("/", async (CreateCommitmentRequest req, MemoryDbContext db) =>
        {
            var commitment = new Commitment
            {
                Description = req.Description,
                OwnerId = req.OwnerId,
                DueDate = req.DueDate,
                Confidence = req.Confidence
            };

            db.Commitments.Add(commitment);

            if (req.SourceIds is { Length: > 0 })
            {
                foreach (var sourceId in req.SourceIds)
                {
                    db.CommitmentSources.Add(new CommitmentSource
                    {
                        CommitmentId = commitment.Id,
                        SourceId = sourceId
                    });
                }
            }

            db.CommitmentStatusHistories.Add(new CommitmentStatusHistory
            {
                CommitmentId = commitment.Id,
                Status = "open",
                Notes = "Initial status"
            });

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Commitment",
                EntityId = commitment.Id,
                Action = "created",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();

            return Results.Created($"/commitments/{commitment.Id}", new { id = commitment.Id });
        });

        group.MapGet("/", async (string? status, MemoryDbContext db) =>
        {
            var query = db.Commitments
                .Include(c => c.Sources)
                .Include(c => c.StatusHistory)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(c => c.Status == status);

            var commitments = await query.ToListAsync();
            return Results.Ok(commitments);
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var commitment = await db.Commitments
                .Include(c => c.Sources)
                .Include(c => c.StatusHistory)
                .FirstOrDefaultAsync(c => c.Id == id);

            return commitment is null ? Results.NotFound() : Results.Ok(commitment);
        });

        group.MapPut("/{id:guid}/status", async (Guid id, UpdateCommitmentStatusRequest req, MemoryDbContext db) =>
        {
            var commitment = await db.Commitments.FindAsync(id);
            if (commitment is null) return Results.NotFound();

            commitment.Status = req.Status;

            db.CommitmentStatusHistories.Add(new CommitmentStatusHistory
            {
                CommitmentId = id,
                Status = req.Status,
                Notes = req.Notes ?? string.Empty
            });

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Commitment",
                EntityId = commitment.Id,
                Action = "updated",
                ChangedBy = "api"
            });

            await db.SaveChangesAsync();

            return Results.Ok(new { id = commitment.Id, status = commitment.Status });
        });
    }
}

public record CreateCommitmentRequest(
    string Description,
    Guid? OwnerId = null,
    DateTime? DueDate = null,
    float Confidence = 1.0f,
    Guid[]? SourceIds = null);

public record UpdateCommitmentStatusRequest(string Status, string? Notes = null);
