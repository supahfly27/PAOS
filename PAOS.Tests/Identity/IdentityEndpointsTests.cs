using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Identity;

[Collection("Integration")]
public class IdentityEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.UserPreferences.RemoveRange(db.UserPreferences);
        db.UserGoals.RemoveRange(db.UserGoals);
        db.UserValues.RemoveRange(db.UserValues);
        db.UserHabits.RemoveRange(db.UserHabits);
        db.UserProfiles.RemoveRange(db.UserProfiles);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostIdentity_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/identity", new
        {
            displayName = "David",
            timezone = "UTC",
            communicationStyle = "direct"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString();
        Assert.NotNull(id);
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public async Task GetIdentity_ReturnsProfileWithCollections()
    {
        var postResponse = await _client.PostAsJsonAsync("/identity", new
        {
            displayName = "Alice",
            timezone = "America/New_York",
            communicationStyle = "formal"
        });
        var created = await postResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var getResponse = await _client.GetAsync($"/identity/{id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var profile = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alice", profile.GetProperty("displayName").GetString());
        Assert.Equal("America/New_York", profile.GetProperty("timezone").GetString());
        Assert.Equal(JsonValueKind.Array, profile.GetProperty("preferences").ValueKind);
        Assert.Equal(JsonValueKind.Array, profile.GetProperty("goals").ValueKind);
    }

    [Fact]
    public async Task PostPreference_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/identity", new { displayName = "Bob", timezone = (string?)null, communicationStyle = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var prefResponse = await _client.PostAsJsonAsync($"/identity/{id}/preferences", new
        {
            key = "theme",
            value = "dark"
        });
        Assert.Equal(HttpStatusCode.Created, prefResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/identity/{id}");
        var profile = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var prefs = profile.GetProperty("preferences");
        Assert.Equal(1, prefs.GetArrayLength());
        Assert.Equal("theme", prefs[0].GetProperty("key").GetString());
        Assert.Equal("dark", prefs[0].GetProperty("value").GetString());
    }

    [Fact]
    public async Task PostGoal_ThenUpdateStatus_Works()
    {
        var created = await (await _client.PostAsJsonAsync("/identity", new { displayName = "Carol", timezone = (string?)null, communicationStyle = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var profileId = created.GetProperty("id").GetString()!;

        var goalResponse = await _client.PostAsJsonAsync($"/identity/{profileId}/goals", new
        {
            goal = "Ship PAOS Phase 3",
            priority = 1
        });
        Assert.Equal(HttpStatusCode.Created, goalResponse.StatusCode);
        var goalCreated = await goalResponse.Content.ReadFromJsonAsync<JsonElement>();
        var goalId = goalCreated.GetProperty("id").GetString()!;

        var updateResponse = await _client.PutAsJsonAsync($"/identity/{profileId}/goals/{goalId}", new
        {
            status = "completed"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", updated.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteIdentity_Returns204_AndAuditLogWritten()
    {
        var created = await (await _client.PostAsJsonAsync("/identity", new { displayName = "TempUser", timezone = (string?)null, communicationStyle = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var deleteResponse = await _client.DeleteAsync($"/identity/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var auditLogs = db.AuditLogs.Where(a => a.EntityType == "UserProfile").ToList();
        Assert.Contains(auditLogs, a => a.Action == "deleted");
    }

    [Fact]
    public async Task GetIdentity_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/identity/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
