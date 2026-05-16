using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Episodic;

[Collection("E2E")]
public class EpisodicE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "EventSummaries", "EventSources", "EventParticipants", "MemoryEvents",
            "SourceChunks", "Sources", "People");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateEvent_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/events", new
        {
            type = "meeting",
            summary = "Kickoff meeting with stakeholders"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "MemoryEvents", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetEvent_ReturnsWithNavProperties()
    {
        var postRes = await f.Http.PostAsJsonAsync("/events", new
        {
            type = "call",
            summary = "Weekly sync"
        });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var getRes = await f.Http.GetAsync($"/events/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var ev = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Weekly sync", ev.GetProperty("summary").GetString());
        Assert.Equal(JsonValueKind.Array, ev.GetProperty("participants").ValueKind);
        Assert.Equal(JsonValueKind.Array, ev.GetProperty("summaries").ValueKind);
    }

    [Fact]
    public async Task AddParticipant_Returns201AndAppearsInDB()
    {
        var eventRes = await f.Http.PostAsJsonAsync("/events", new { type = "meeting", summary = "Design review" });
        var eventBody = await eventRes.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = eventBody.GetProperty("id").GetString()!;

        var personRes = await f.Http.PostAsJsonAsync("/people", new { name = "Alice" });
        var personBody = await personRes.Content.ReadFromJsonAsync<JsonElement>();
        var personId = personBody.GetProperty("id").GetString()!;

        var partRes = await f.Http.PostAsJsonAsync($"/events/{eventId}/participants", new
        {
            personId,
            role = "reviewer"
        });
        Assert.Equal(HttpStatusCode.Created, partRes.StatusCode);

        var partBody = await partRes.Content.ReadFromJsonAsync<JsonElement>();
        var partId = Guid.Parse(partBody.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "EventParticipants", "\"Id\" = @id",
            new NpgsqlParameter("id", partId));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddEventSource_LinksSourceToEvent()
    {
        var eventRes = await f.Http.PostAsJsonAsync("/events", new { type = "workshop", summary = "Planning workshop" });
        var eventBody = await eventRes.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = eventBody.GetProperty("id").GetString()!;

        var sourceRes = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "Workshop agenda",
            extractionMethod = "manual",
            confidence = 1.0
        });
        var sourceBody = await sourceRes.Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = sourceBody.GetProperty("id").GetString()!;

        var linkRes = await f.Http.PostAsJsonAsync($"/events/{eventId}/sources", new { sourceId });
        Assert.Equal(HttpStatusCode.Created, linkRes.StatusCode);
    }

    [Fact]
    public async Task AddSummary_Returns201()
    {
        var eventRes = await f.Http.PostAsJsonAsync("/events", new { type = "retro", summary = "Sprint retro" });
        var eventBody = await eventRes.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = eventBody.GetProperty("id").GetString()!;

        var summaryRes = await f.Http.PostAsJsonAsync($"/events/{eventId}/summaries", new
        {
            summary = "Team agreed to improve PR review turnaround"
        });
        Assert.Equal(HttpStatusCode.Created, summaryRes.StatusCode);
    }
}
