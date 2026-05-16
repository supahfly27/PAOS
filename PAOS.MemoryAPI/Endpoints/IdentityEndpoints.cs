using Microsoft.EntityFrameworkCore;
using PAOS.Data;
using PAOS.Data.Entities.Identity;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/identity");

        group.MapPost("/", async (CreateProfileRequest req, MemoryDbContext db) =>
        {
            var profile = new UserProfile
            {
                DisplayName = req.DisplayName,
                Timezone = req.Timezone ?? "UTC",
                CommunicationStyle = req.CommunicationStyle ?? string.Empty
            };

            db.UserProfiles.Add(profile);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "UserProfile",
                EntityId = profile.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/identity/{profile.Id}", new { id = profile.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var profile = await db.UserProfiles
                .Include(p => p.Preferences)
                .Include(p => p.Goals)
                .Include(p => p.Values)
                .Include(p => p.Habits)
                .FirstOrDefaultAsync(p => p.Id == id);

            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        group.MapPost("/{id:guid}/preferences", async (Guid id, AddPreferenceRequest req, MemoryDbContext db) =>
        {
            var profile = await db.UserProfiles.FindAsync(id);
            if (profile is null) return Results.NotFound();

            var pref = new UserPreference
            {
                UserProfileId = id,
                Key = req.Key,
                Value = req.Value
            };

            db.UserPreferences.Add(pref);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "UserPreference",
                EntityId = pref.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/identity/{id}/preferences/{pref.Id}", new { id = pref.Id });
        });

        group.MapPost("/{id:guid}/goals", async (Guid id, AddGoalRequest req, MemoryDbContext db) =>
        {
            var profile = await db.UserProfiles.FindAsync(id);
            if (profile is null) return Results.NotFound();

            var goal = new UserGoal
            {
                UserProfileId = id,
                Goal = req.Goal,
                Priority = req.Priority
            };

            db.UserGoals.Add(goal);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "UserGoal",
                EntityId = goal.Id,
                Action = "created",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/identity/{id}/goals/{goal.Id}", new { id = goal.Id });
        });

        group.MapPut("/{id:guid}/goals/{goalId:guid}", async (Guid id, Guid goalId, UpdateGoalStatusRequest req, MemoryDbContext db) =>
        {
            var goal = await db.UserGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserProfileId == id);
            if (goal is null) return Results.NotFound();

            goal.Status = req.Status;

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "UserGoal",
                EntityId = goal.Id,
                Action = "updated",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.Ok(new { id = goal.Id, status = goal.Status });
        });

        group.MapDelete("/{id:guid}", async (Guid id, MemoryDbContext db) =>
        {
            var profile = await db.UserProfiles.FindAsync(id);
            if (profile is null) return Results.NotFound();

            profile.UpdatedAt = DateTime.UtcNow;

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "UserProfile",
                EntityId = profile.Id,
                Action = "deleted",
                ChangedBy = "api"
            });
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}

public record CreateProfileRequest(string DisplayName, string? Timezone, string? CommunicationStyle);
public record AddPreferenceRequest(string Key, string Value);
public record AddGoalRequest(string Goal, int Priority = 0);
public record UpdateGoalStatusRequest(string Status);
