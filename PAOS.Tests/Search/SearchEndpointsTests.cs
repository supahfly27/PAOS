using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Search;

[Collection("Integration")]
public class SearchEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.MemoryEmbeddings.RemoveRange(db.MemoryEmbeddings);
        db.SourceChunks.RemoveRange(db.SourceChunks);
        db.Sources.RemoveRange(db.Sources);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task KeywordSearch_ReturnsMatchingSources()
    {
        await _client.PostAsJsonAsync("/sources", new
        {
            type = "text",
            rawContent = "Alice will deliver the report by Thursday",
            extractionMethod = "manual"
        });
        await _client.PostAsJsonAsync("/sources", new
        {
            type = "text",
            rawContent = "The budget meeting is next Monday",
            extractionMethod = "manual"
        });

        var response = await _client.PostAsJsonAsync("/search/keyword", new { query = "report" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, results.GetArrayLength());
        Assert.Contains("report", results[0].GetProperty("rawContent").GetString()!);
    }

    [Fact]
    public async Task KeywordSearch_CaseInsensitive()
    {
        await _client.PostAsJsonAsync("/sources", new
        {
            type = "text",
            rawContent = "Budget review with Alice",
            extractionMethod = "manual"
        });

        var response = await _client.PostAsJsonAsync("/search/keyword", new { query = "BUDGET" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, results.GetArrayLength());
    }

    [Fact]
    public async Task KeywordSearch_NoMatch_ReturnsEmpty()
    {
        await _client.PostAsJsonAsync("/sources", new
        {
            type = "text",
            rawContent = "Project kickoff scheduled",
            extractionMethod = "manual"
        });

        var response = await _client.PostAsJsonAsync("/search/keyword", new { query = "zyzzyva" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, results.GetArrayLength());
    }

    [Fact]
    public async Task KeywordSearch_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/sources", new
            {
                type = "text",
                rawContent = $"Meeting note {i} with agenda",
                extractionMethod = "manual"
            });
        }

        var response = await _client.PostAsJsonAsync("/search/keyword", new { query = "agenda", limit = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, results.GetArrayLength());
    }

    [Fact]
    public async Task SemanticSearch_WithoutApiKey_Returns503()
    {
        // The test factory doesn't set OpenAI:ApiKey, so the endpoint should return 503
        var apiKey = Environment.GetEnvironmentVariable("OPENAI__APIKEY")
                  ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey");
        if (!string.IsNullOrEmpty(apiKey))
            return; // key is present — endpoint will attempt real call, skip this test

        var response = await _client.PostAsJsonAsync("/search", new { query = "find meeting notes" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
