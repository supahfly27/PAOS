using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Procedural;

[Collection("Integration")]
public class ProceduralEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.ProcedureFeedback.RemoveRange(db.ProcedureFeedback);
        db.ProcedureRuns.RemoveRange(db.ProcedureRuns);
        db.ProcedureSteps.RemoveRange(db.ProcedureSteps);
        db.Procedures.RemoveRange(db.Procedures);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostProcedure_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/procedures", new
        {
            name = "Deploy to production",
            description = "Standard deployment checklist"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task GetProcedure_ReturnsWithStepsOrdered()
    {
        var created = await (await _client.PostAsJsonAsync("/procedures", new
        {
            name = "Onboarding",
            description = "New employee onboarding"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        await _client.PostAsJsonAsync($"/procedures/{id}/steps", new { stepOrder = 2, action = "Set up laptop" });
        await _client.PostAsJsonAsync($"/procedures/{id}/steps", new { stepOrder = 1, action = "Sign NDA" });

        var response = await _client.GetAsync($"/procedures/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var procedure = await response.Content.ReadFromJsonAsync<JsonElement>();
        var steps = procedure.GetProperty("steps");
        Assert.Equal(2, steps.GetArrayLength());
        Assert.Equal("Sign NDA", steps[0].GetProperty("action").GetString());
        Assert.Equal("Set up laptop", steps[1].GetProperty("action").GetString());
    }

    [Fact]
    public async Task PostRun_ThenComplete_ShowsCompletedAt()
    {
        var created = await (await _client.PostAsJsonAsync("/procedures", new
        {
            name = "Backup DB"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var runResponse = await _client.PostAsJsonAsync($"/procedures/{id}/runs", new { });
        Assert.Equal(HttpStatusCode.Created, runResponse.StatusCode);
        var run = await runResponse.Content.ReadFromJsonAsync<JsonElement>();
        var runId = run.GetProperty("id").GetString()!;

        var completeResponse = await _client.PutAsJsonAsync($"/procedures/{id}/runs/{runId}/complete", new { });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", completed.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, completed.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task PostFeedback_Returns201()
    {
        var created = await (await _client.PostAsJsonAsync("/procedures", new
        {
            name = "Code review"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var runCreated = await (await _client.PostAsJsonAsync($"/procedures/{id}/runs", new { }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var runId = runCreated.GetProperty("id").GetString()!;

        var feedbackResponse = await _client.PostAsJsonAsync($"/procedures/{id}/runs/{runId}/feedback", new
        {
            rating = 4,
            notes = "Smooth process"
        });
        Assert.Equal(HttpStatusCode.Created, feedbackResponse.StatusCode);
    }

    [Fact]
    public async Task GetProcedure_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/procedures/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
