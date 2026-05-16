using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Decisions;

[Collection("E2E")]
public class DecisionsE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "DecisionOutcomes", "DecisionAssumptions", "DecisionOptions", "Decisions");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateDecision_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/decisions", new
        {
            title = "Choose message broker",
            description = "Evaluate Redis vs RabbitMQ vs Kafka"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "Decisions", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetDecision_ReturnsWithNavProperties()
    {
        var postRes = await f.Http.PostAsJsonAsync("/decisions", new { title = "Select database" });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var getRes = await f.Http.GetAsync($"/decisions/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var decision = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Select database", decision.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Array, decision.GetProperty("options").ValueKind);
        Assert.Equal(JsonValueKind.Array, decision.GetProperty("assumptions").ValueKind);
        Assert.Equal(JsonValueKind.Array, decision.GetProperty("outcomes").ValueKind);
    }

    [Fact]
    public async Task AddOptions_AppearInGet()
    {
        var postRes = await f.Http.PostAsJsonAsync("/decisions", new { title = "Hosting platform" });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        await f.Http.PostAsJsonAsync($"/decisions/{id}/options", new { optionText = "AWS", wasChosen = false });
        await f.Http.PostAsJsonAsync($"/decisions/{id}/options", new { optionText = "Azure", wasChosen = true });

        var getRes = await f.Http.GetAsync($"/decisions/{id}");
        var decision = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, decision.GetProperty("options").GetArrayLength());
    }

    [Fact]
    public async Task AddAndInvalidateAssumption_StillValidFlips()
    {
        var postRes = await f.Http.PostAsJsonAsync("/decisions", new { title = "Caching strategy" });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var assumRes = await f.Http.PostAsJsonAsync($"/decisions/{id}/assumptions", new
        {
            assumptionText = "Traffic is under 1000 RPS"
        });
        Assert.Equal(HttpStatusCode.Created, assumRes.StatusCode);
        var assumBody = await assumRes.Content.ReadFromJsonAsync<JsonElement>();
        var assumId = assumBody.GetProperty("id").GetString()!;

        var invalidateRes = await f.Http.PutAsync($"/decisions/{id}/assumptions/{assumId}/invalidate", null);
        Assert.Equal(HttpStatusCode.OK, invalidateRes.StatusCode);

        var result = await invalidateRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("stillValid").GetBoolean());
    }

    [Fact]
    public async Task AddOutcome_Returns201AndAppearsInGet()
    {
        var postRes = await f.Http.PostAsJsonAsync("/decisions", new { title = "API framework" });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var outcomeRes = await f.Http.PostAsJsonAsync($"/decisions/{id}/outcomes", new
        {
            outcomeDescription = "Minimal APIs reduced boilerplate by 40%"
        });
        Assert.Equal(HttpStatusCode.Created, outcomeRes.StatusCode);

        var getRes = await f.Http.GetAsync($"/decisions/{id}");
        var decision = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, decision.GetProperty("outcomes").GetArrayLength());
    }
}
