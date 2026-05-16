using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Semantic;

[Collection("E2E")]
public class SemanticE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "FactConfidenceHistories", "FactSources", "Facts");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateFact_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/facts", new
        {
            subject = "Alice",
            predicate = "works-at",
            @object = "ACME Corp",
            confidence = 0.95
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "Facts", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateFact_SeedsInitialConfidenceHistory()
    {
        var res = await f.Http.PostAsJsonAsync("/facts", new
        {
            subject = "Bob",
            predicate = "prefers",
            @object = "async communication",
            confidence = 0.8
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "FactConfidenceHistories",
            "\"FactId\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetFacts_ReturnsList_WithSubjectFilter()
    {
        await f.Http.PostAsJsonAsync("/facts", new
        {
            subject = "Carol",
            predicate = "likes",
            @object = "coffee",
            confidence = 1.0
        });
        await f.Http.PostAsJsonAsync("/facts", new
        {
            subject = "Dave",
            predicate = "uses",
            @object = "vim",
            confidence = 1.0
        });

        var allRes = await f.Http.GetAsync("/facts");
        var all = await allRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(all.GetArrayLength() >= 2);

        var carolRes = await f.Http.GetAsync("/facts?subject=Carol");
        var carolFacts = await carolRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, carolFacts.GetArrayLength());
        Assert.Equal("Carol", carolFacts[0].GetProperty("subject").GetString());
    }

    [Fact]
    public async Task GetFact_Returns200WithData()
    {
        var postRes = await f.Http.PostAsJsonAsync("/facts", new
        {
            subject = "System",
            predicate = "runs-on",
            @object = "Linux",
            confidence = 1.0
        });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var getRes = await f.Http.GetAsync($"/facts/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var fact = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("System", fact.GetProperty("subject").GetString());
        Assert.Equal("Linux", fact.GetProperty("object").GetString());
    }

    [Fact]
    public async Task UpdateConfidence_AppendsHistoryRow()
    {
        var postRes = await f.Http.PostAsJsonAsync("/facts", new
        {
            subject = "Eve",
            predicate = "knows",
            @object = "C#",
            confidence = 0.7
        });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var updateRes = await f.Http.PutAsJsonAsync($"/facts/{id}/confidence", new { confidence = 0.95 });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.95f, updated.GetProperty("confidence").GetSingle(), 2);

        var histCount = await CleanupHelper.CountRowsAsync(f.Db, "FactConfidenceHistories",
            "\"FactId\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(2, histCount); // initial + update
    }
}
