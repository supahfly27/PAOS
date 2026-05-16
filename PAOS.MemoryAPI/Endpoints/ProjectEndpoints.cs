using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Projects;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/projects");

        group.MapPost("/", async (CreateProjectRequest req, MemoryDbContext db) =>
        {
            var project = new Project
            {
                Name = req.Name,
                Description = req.Description ?? string.Empty
            };

            db.Projects.Add(project);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Project",
                EntityId = project.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/projects/{project.Id}", new { id = project.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var project = await db.Projects
                .Include(p => p.Members)
                .Include(p => p.Blockers)
                .Include(p => p.StatusUpdates)
                .Include(p => p.Files)
                .FirstOrDefaultAsync(p => p.Id == id);

            return project is null ? Results.NotFound() : Results.Ok(project);
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest req, MemoryDbContext db) =>
        {
            var project = await db.Projects.FindAsync(id);
            if (project is null) return Results.NotFound();

            var member = new ProjectMember
            {
                ProjectId = id,
                PersonId = req.PersonId,
                Role = req.Role ?? string.Empty
            };

            db.ProjectMembers.Add(member);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProjectMember",
                EntityId = member.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/projects/{id}/members/{member.Id}", new { id = member.Id });
        });

        group.MapPost("/{id:guid}/blockers", async (Guid id, AddBlockerRequest req, MemoryDbContext db) =>
        {
            var project = await db.Projects.FindAsync(id);
            if (project is null) return Results.NotFound();

            var blocker = new ProjectBlocker
            {
                ProjectId = id,
                Description = req.Description
            };

            db.ProjectBlockers.Add(blocker);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProjectBlocker",
                EntityId = blocker.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/projects/{id}/blockers/{blocker.Id}", new { id = blocker.Id });
        });

        group.MapPut("/{id:guid}/blockers/{blockerId:guid}/resolve", async (Guid id, Guid blockerId, MemoryDbContext db) =>
        {
            var blocker = await db.ProjectBlockers.FirstOrDefaultAsync(b => b.Id == blockerId && b.ProjectId == id);
            if (blocker is null) return Results.NotFound();

            blocker.ResolvedAt = DateTime.UtcNow;

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProjectBlocker",
                EntityId = blocker.Id,
                Action = "resolved",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Ok(new { id = blocker.Id, resolvedAt = blocker.ResolvedAt });
        });

        group.MapPost("/{id:guid}/status", async (Guid id, PostStatusRequest req, MemoryDbContext db) =>
        {
            var project = await db.Projects.FindAsync(id);
            if (project is null) return Results.NotFound();

            project.Status = req.Status;

            var update = new ProjectStatusUpdate
            {
                ProjectId = id,
                Status = req.Status,
                Summary = req.Summary ?? string.Empty
            };

            db.ProjectStatusUpdates.Add(update);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProjectStatusUpdate",
                EntityId = update.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/projects/{id}/status/{update.Id}", new { id = update.Id });
        });

        group.MapPost("/{id:guid}/files", async (Guid id, RegisterFileRequest req, MemoryDbContext db) =>
        {
            var project = await db.Projects.FindAsync(id);
            if (project is null) return Results.NotFound();

            var file = new ProjectFile
            {
                ProjectId = id,
                FileKey = req.FileKey,
                Filename = req.Filename
            };

            db.ProjectFiles.Add(file);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProjectFile",
                EntityId = file.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/projects/{id}/files/{file.Id}", new { id = file.Id });
        });
    }
}

public record CreateProjectRequest(string Name, string? Description = null);
public record AddMemberRequest(Guid PersonId, string? Role = null);
public record AddBlockerRequest(string Description);
public record PostStatusRequest(string Status, string? Summary = null);
public record RegisterFileRequest(string FileKey, string Filename);
