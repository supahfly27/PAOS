using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Procedural;

[Collection("E2E")]
public class ProceduralE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "ProcedureFeedback", "ProcedureRuns", "ProcedureSteps", "Procedures");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateProcedure_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/procedures", new
        {
            name = "Deploy to production",
            description = "Standard production deployment checklist"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "Procedures", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProcedure_ReturnsWithStepsOrderedAndRuns()
    {
        var procRes = await f.Http.PostAsJsonAsync("/procedures", new { name = "Onboarding" });
        var procBody = await procRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = procBody.GetProperty("id").GetString()!;

        await f.Http.PostAsJsonAsync($"/procedures/{id}/steps", new { stepOrder = 2, action = "Grant access" });
        await f.Http.PostAsJsonAsync($"/procedures/{id}/steps", new { stepOrder = 1, action = "Send invite" });

        var getRes = await f.Http.GetAsync($"/procedures/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var proc = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Onboarding", proc.GetProperty("name").GetString());
        var steps = proc.GetProperty("steps");
        Assert.Equal(2, steps.GetArrayLength());
        Assert.Equal(1, steps[0].GetProperty("stepOrder").GetInt32());
        Assert.Equal(2, steps[1].GetProperty("stepOrder").GetInt32());
    }

    [Fact]
    public async Task StartAndCompleteRun_UpdatesStatus()
    {
        var procRes = await f.Http.PostAsJsonAsync("/procedures", new { name = "Backup DB" });
        var procBody = await procRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = procBody.GetProperty("id").GetString()!;

        var runRes = await f.Http.PostAsync($"/procedures/{id}/runs", null);
        Assert.Equal(HttpStatusCode.Created, runRes.StatusCode);
        var runBody = await runRes.Content.ReadFromJsonAsync<JsonElement>();
        var runId = runBody.GetProperty("id").GetString()!;

        var completeRes = await f.Http.PutAsync($"/procedures/{id}/runs/{runId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);

        var completed = await completeRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", completed.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, completed.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task AddFeedback_Returns201AndAppearsInDB()
    {
        var procRes = await f.Http.PostAsJsonAsync("/procedures", new { name = "Code review" });
        var procBody = await procRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = procBody.GetProperty("id").GetString()!;

        var runRes = await f.Http.PostAsync($"/procedures/{id}/runs", null);
        var runBody = await runRes.Content.ReadFromJsonAsync<JsonElement>();
        var runId = Guid.Parse(runBody.GetProperty("id").GetString()!);

        var feedbackRes = await f.Http.PostAsJsonAsync($"/procedures/{id}/runs/{runId}/feedback", new
        {
            rating = 4,
            notes = "Went smoothly"
        });
        Assert.Equal(HttpStatusCode.Created, feedbackRes.StatusCode);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "ProcedureFeedback",
            "\"ProcedureRunId\" = @id",
            new NpgsqlParameter("id", runId));
        Assert.Equal(1, count);
    }
}
