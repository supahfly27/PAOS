using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Semantic;

[Collection("Integration")]
public class SemanticEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.FactConfidenceHistories.RemoveRange(db.FactConfidenceHistories);
        db.FactSources.RemoveRange(db.FactSources);
        db.Facts.RemoveRange(db.Facts);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostFact_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/facts", new
        {
            subject = "Alice",
            predicate = "worksAt",
            @object = "Acme Corp"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task GetFact_ReturnsWithSources()
    {
        var created = await (await _client.PostAsJsonAsync("/facts", new
        {
            subject = "Bob",
            predicate = "livesIn",
            @object = "Seattle"
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/facts/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fact = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bob", fact.GetProperty("subject").GetString());
        Assert.Equal(JsonValueKind.Array, fact.GetProperty("sources").ValueKind);
    }

    [Fact]
    public async Task ListFacts_FilterBySubject()
    {
        await _client.PostAsJsonAsync("/facts", new { subject = "Alice", predicate = "worksAt", @object = "Acme" });
        await _client.PostAsJsonAsync("/facts", new { subject = "Alice", predicate = "livesIn", @object = "NYC" });
        await _client.PostAsJsonAsync("/facts", new { subject = "Bob", predicate = "worksAt", @object = "BigCo" });

        var response = await _client.GetAsync("/facts?subject=Alice");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetArrayLength());
    }

    [Fact]
    public async Task UpdateConfidence_AppendsHistory()
    {
        var created = await (await _client.PostAsJsonAsync("/facts", new
        {
            subject = "Carol",
            predicate = "owns",
            @object = "cats",
            confidence = 0.8f
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var updateResponse = await _client.PutAsJsonAsync($"/facts/{id}/confidence", new
        {
            confidence = 0.5f
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.5f, updated.GetProperty("confidence").GetSingle(), 3);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var historyCount = db.FactConfidenceHistories.Count(h => h.FactId == Guid.Parse(id));
        Assert.Equal(2, historyCount);
    }

    [Fact]
    public async Task GetFact_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/facts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
