using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Commitments;

[Collection("Integration")]
public class CommitmentEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.CommitmentStatusHistories.RemoveRange(db.CommitmentStatusHistories);
        db.CommitmentSources.RemoveRange(db.CommitmentSources);
        db.Commitments.RemoveRange(db.Commitments);
        db.SourceChunks.RemoveRange(db.SourceChunks);
        db.Sources.RemoveRange(db.Sources);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostCommitment_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/commitments", new
        {
            description = "Deliver the Q2 report by Friday"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task PostCommitment_WithSourceId_LinksSource()
    {
        var sourceCreated = await (await _client.PostAsJsonAsync("/sources", new
        {
            type = "text",
            rawContent = "I promised Alice the budget by Monday",
            extractionMethod = "manual"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = sourceCreated.GetProperty("id").GetString()!;

        var response = await _client.PostAsJsonAsync("/commitments", new
        {
            description = "Send budget to Alice",
            sourceIds = new[] { sourceId }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var getResponse = await _client.GetAsync($"/commitments/{id}");
        var commitment = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sources = commitment.GetProperty("sources");
        Assert.Equal(1, sources.GetArrayLength());
        Assert.Equal(sourceId, sources[0].GetProperty("sourceId").GetString());
    }

    [Fact]
    public async Task ListCommitments_FilterByStatus()
    {
        await _client.PostAsJsonAsync("/commitments", new { description = "Open commitment A" });
        await _client.PostAsJsonAsync("/commitments", new { description = "Open commitment B" });

        var response = await _client.GetAsync("/commitments?status=open");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetArrayLength());

        var emptyResponse = await _client.GetAsync("/commitments?status=completed");
        var emptyList = await emptyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, emptyList.GetArrayLength());
    }

    [Fact]
    public async Task UpdateStatus_AppendsToHistory()
    {
        var created = await (await _client.PostAsJsonAsync("/commitments", new { description = "Deliver proposal" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var updateResponse = await _client.PutAsJsonAsync($"/commitments/{id}/status", new
        {
            status = "completed",
            notes = "Delivered on time"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", updated.GetProperty("status").GetString());

        var getResponse = await _client.GetAsync($"/commitments/{id}");
        var commitment = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", commitment.GetProperty("status").GetString());
        var history = commitment.GetProperty("statusHistory");
        Assert.Equal(2, history.GetArrayLength());
    }

    [Fact]
    public async Task GetCommitment_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/commitments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
