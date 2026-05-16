using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Identity;

[Collection("E2E")]
public class IdentityE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "UserPreferences", "UserGoals", "UserValues", "UserHabits", "UserProfiles");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateProfile_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/identity", new
        {
            displayName = "Alice",
            timezone = "America/New_York",
            communicationStyle = "direct"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "UserProfiles", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProfile_ReturnsNavProperties()
    {
        var postRes = await f.Http.PostAsJsonAsync("/identity", new
        {
            displayName = "Bob",
            timezone = "UTC",
            communicationStyle = "verbose"
        });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var getRes = await f.Http.GetAsync($"/identity/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var profile = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bob", profile.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Array, profile.GetProperty("preferences").ValueKind);
        Assert.Equal(JsonValueKind.Array, profile.GetProperty("goals").ValueKind);
    }

    [Fact]
    public async Task AddPreference_Returns201AndAppearsInGet()
    {
        var profileRes = await f.Http.PostAsJsonAsync("/identity", new
        {
            displayName = "Charlie",
            timezone = "UTC",
            communicationStyle = ""
        });
        var profileBody = await profileRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = profileBody.GetProperty("id").GetString()!;

        var prefRes = await f.Http.PostAsJsonAsync($"/identity/{id}/preferences", new
        {
            key = "theme",
            value = "dark"
        });
        Assert.Equal(HttpStatusCode.Created, prefRes.StatusCode);

        var getRes = await f.Http.GetAsync($"/identity/{id}");
        var profile = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        var prefs = profile.GetProperty("preferences");
        Assert.True(prefs.GetArrayLength() >= 1);
        Assert.Equal("dark", prefs[0].GetProperty("value").GetString());
    }

    [Fact]
    public async Task AddAndUpdateGoal_StatusChanges()
    {
        var profileRes = await f.Http.PostAsJsonAsync("/identity", new
        {
            displayName = "Dave",
            timezone = "UTC",
            communicationStyle = ""
        });
        var profileBody = await profileRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = profileBody.GetProperty("id").GetString()!;

        var goalRes = await f.Http.PostAsJsonAsync($"/identity/{id}/goals", new
        {
            goal = "Ship Phase 5",
            priority = 1
        });
        Assert.Equal(HttpStatusCode.Created, goalRes.StatusCode);
        var goalBody = await goalRes.Content.ReadFromJsonAsync<JsonElement>();
        var goalId = goalBody.GetProperty("id").GetString()!;

        var updateRes = await f.Http.PutAsJsonAsync($"/identity/{id}/goals/{goalId}", new { status = "done" });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("done", updated.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteProfile_Returns204()
    {
        var profileRes = await f.Http.PostAsJsonAsync("/identity", new
        {
            displayName = "Eve",
            timezone = "UTC",
            communicationStyle = ""
        });
        var profileBody = await profileRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = profileBody.GetProperty("id").GetString()!;

        var deleteRes = await f.Http.DeleteAsync($"/identity/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
    }
}
