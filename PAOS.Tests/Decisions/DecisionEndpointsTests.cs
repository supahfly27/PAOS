using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Decisions;

[Collection("Integration")]
public class DecisionEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.DecisionOutcomes.RemoveRange(db.DecisionOutcomes);
        db.DecisionAssumptions.RemoveRange(db.DecisionAssumptions);
        db.DecisionOptions.RemoveRange(db.DecisionOptions);
        db.Decisions.RemoveRange(db.Decisions);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostDecision_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/decisions", new
        {
            title = "Choose cloud provider",
            description = "AWS vs Azure vs GCP"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task GetDecision_ReturnsWithCollections()
    {
        var created = await (await _client.PostAsJsonAsync("/decisions", new
        {
            title = "Tech stack selection",
            description = "Choosing the primary language"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/decisions/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tech stack selection", decision.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Array, decision.GetProperty("options").ValueKind);
        Assert.Equal(JsonValueKind.Array, decision.GetProperty("assumptions").ValueKind);
        Assert.Equal(JsonValueKind.Array, decision.GetProperty("outcomes").ValueKind);
    }

    [Fact]
    public async Task PostOptions_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/decisions", new
        {
            title = "Database selection"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        await _client.PostAsJsonAsync($"/decisions/{id}/options", new { optionText = "PostgreSQL", wasChosen = true });
        await _client.PostAsJsonAsync($"/decisions/{id}/options", new { optionText = "MySQL", wasChosen = false });

        var response = await _client.GetAsync($"/decisions/{id}");
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>();
        var options = decision.GetProperty("options");
        Assert.Equal(2, options.GetArrayLength());
    }

    [Fact]
    public async Task InvalidateAssumption_SetsStillValidFalse()
    {
        var created = await (await _client.PostAsJsonAsync("/decisions", new
        {
            title = "Remote work policy"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var assumptionCreated = await (await _client.PostAsJsonAsync($"/decisions/{id}/assumptions", new
        {
            assumptionText = "Team prefers async communication"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var assumptionId = assumptionCreated.GetProperty("id").GetString()!;

        var invalidateResponse = await _client.PutAsJsonAsync(
            $"/decisions/{id}/assumptions/{assumptionId}/invalidate", new { });
        Assert.Equal(HttpStatusCode.OK, invalidateResponse.StatusCode);
        var result = await invalidateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("stillValid").GetBoolean());
    }

    [Fact]
    public async Task PostOutcome_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/decisions", new
        {
            title = "API framework choice"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        await _client.PostAsJsonAsync($"/decisions/{id}/outcomes", new
        {
            outcomeDescription = "Shipped 2 weeks ahead of schedule"
        });

        var response = await _client.GetAsync($"/decisions/{id}");
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>();
        var outcomes = decision.GetProperty("outcomes");
        Assert.Equal(1, outcomes.GetArrayLength());
        Assert.Equal("Shipped 2 weeks ahead of schedule", outcomes[0].GetProperty("outcomeDescription").GetString());
    }

    [Fact]
    public async Task GetDecision_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/decisions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
