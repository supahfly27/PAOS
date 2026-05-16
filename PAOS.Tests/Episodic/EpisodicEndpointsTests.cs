using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Episodic;

[Collection("Integration")]
public class EpisodicEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.EventSummaries.RemoveRange(db.EventSummaries);
        db.EventSources.RemoveRange(db.EventSources);
        db.EventParticipants.RemoveRange(db.EventParticipants);
        db.MemoryEvents.RemoveRange(db.MemoryEvents);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostEvent_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/events", new
        {
            type = "meeting",
            summary = "Quarterly planning session"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task GetEvent_ReturnsWithCollections()
    {
        var created = await (await _client.PostAsJsonAsync("/events", new
        {
            type = "call",
            summary = "Vendor sync",
            occurredAt = DateTime.UtcNow
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/events/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ev = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("call", ev.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, ev.GetProperty("participants").ValueKind);
        Assert.Equal(JsonValueKind.Array, ev.GetProperty("sources").ValueKind);
        Assert.Equal(JsonValueKind.Array, ev.GetProperty("summaries").ValueKind);
    }

    [Fact]
    public async Task PostParticipant_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/events", new
        {
            type = "meeting",
            summary = "Design review"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var personCreated = await (await _client.PostAsJsonAsync("/people", new
        {
            name = "Alice"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var personId = personCreated.GetProperty("id").GetString()!;

        var participantResponse = await _client.PostAsJsonAsync($"/events/{id}/participants", new
        {
            personId,
            role = "presenter"
        });
        Assert.Equal(HttpStatusCode.Created, participantResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/events/{id}");
        var ev = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var participants = ev.GetProperty("participants");
        Assert.Equal(1, participants.GetArrayLength());
        Assert.Equal("presenter", participants[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task PostSummary_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/events", new
        {
            type = "workshop",
            summary = "Team retrospective"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var summaryResponse = await _client.PostAsJsonAsync($"/events/{id}/summaries", new
        {
            summary = "Great discussion, three action items identified"
        });
        Assert.Equal(HttpStatusCode.Created, summaryResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/events/{id}");
        var ev = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var summaries = ev.GetProperty("summaries");
        Assert.Equal(1, summaries.GetArrayLength());
        Assert.Equal("Great discussion, three action items identified", summaries[0].GetProperty("summary").GetString());
    }

    [Fact]
    public async Task GetEvent_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
