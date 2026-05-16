using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Search;

[Collection("E2E")]
public class SearchE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db, "AuditLogs", "MemoryEmbeddings", "SourceChunks", "Sources");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task KeywordSearch_FindsMatchingSource()
    {
        await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "Project Orion planning document",
            extractionMethod = "manual",
            confidence = 1.0
        });

        var res = await f.Http.PostAsJsonAsync("/search/keyword", new { query = "Orion" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var results = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(results.GetArrayLength() >= 1);
        var first = results[0];
        Assert.Contains("Orion", first.GetProperty("rawContent").GetString()!);
    }

    [Fact]
    public async Task KeywordSearch_ReturnsEmpty_WhenNoMatch()
    {
        var res = await f.Http.PostAsJsonAsync("/search/keyword", new { query = "xyzzy_no_match_12345" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var results = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, results.GetArrayLength());
    }

    [Fact]
    public async Task KeywordSearch_IsCaseInsensitive()
    {
        await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "UPPERCASE content for search test",
            extractionMethod = "manual",
            confidence = 1.0
        });

        var res = await f.Http.PostAsJsonAsync("/search/keyword", new { query = "uppercase" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var results = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(results.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task SemanticSearch_Returns503_WhenNoApiKey()
    {
        var res = await f.Http.PostAsJsonAsync("/search", new { query = "test query", limit = 5 });
        // 200 means the container has an OpenAI key — semantic search works, nothing to assert
        if (res.StatusCode == HttpStatusCode.OK) return;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }
}
