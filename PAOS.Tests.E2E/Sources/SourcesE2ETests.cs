using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Sources;

[Collection("E2E")]
public class SourcesE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await f.Redis.KeyDeleteAsync("embed_queue");
        await CleanupHelper.DeleteTablesAsync(f.Db, "AuditLogs", "MemoryEmbeddings", "SourceChunks", "Sources");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostSource_Returns201WithId()
    {
        var res = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "Meeting notes from Monday",
            extractionMethod = "manual",
            confidence = 0.9
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task PostSource_RowAppearsInPostgres()
    {
        var res = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "email",
            rawContent = "Email content for DB verification",
            extractionMethod = "automated",
            confidence = 1.0
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "Sources", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PostSource_AuditLogCreated()
    {
        var res = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "Audit check content",
            extractionMethod = "manual",
            confidence = 1.0
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "AuditLogs",
            "\"EntityType\" = 'Source' AND \"EntityId\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PostSource_PushesJobToRedisOrEmbeddingAppears()
    {
        var queueBefore = await f.Redis.ListLengthAsync("embed_queue");

        var res = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "Content to be embedded",
            extractionMethod = "manual",
            confidence = 1.0
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        await Task.Delay(300); // allow async push to complete

        var queueAfter = await f.Redis.ListLengthAsync("embed_queue");
        var embeddingCount = await CleanupHelper.CountRowsAsync(f.Db, "MemoryEmbeddings",
            "\"EntityType\" = 'Source' AND \"EntityId\" = @id",
            new NpgsqlParameter("id", id));

        Assert.True(queueAfter > queueBefore || embeddingCount > 0,
            "Expected an embed job in the Redis queue or an embedding row in Postgres");
    }

    [Fact]
    public async Task GetSource_Returns200WithContent()
    {
        var postRes = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "doc",
            rawContent = "Retrievable document content",
            extractionMethod = "manual",
            confidence = 0.8
        });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var getRes = await f.Http.GetAsync($"/sources/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var source = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Retrievable document content", source.GetProperty("rawContent").GetString());
        Assert.Equal("doc", source.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetSource_UnknownId_Returns404()
    {
        var res = await f.Http.GetAsync($"/sources/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task EmbeddingPipeline_EventuallyCreatesRow_WhenApiKeyConfigured()
    {
        var hasKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI__APIKEY"))
                     || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OpenAI__ApiKey"));
        if (!hasKey)
        {
            // Skip: no OpenAI key, worker will not embed
            return;
        }

        var res = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "The quick brown fox jumps over the lazy dog",
            extractionMethod = "manual",
            confidence = 1.0
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        // Poll up to 30s for the embedding worker to process the job
        var deadline = DateTime.UtcNow.AddSeconds(30);
        long embeddingCount = 0;
        while (DateTime.UtcNow < deadline)
        {
            embeddingCount = await CleanupHelper.CountRowsAsync(f.Db, "MemoryEmbeddings",
                "\"EntityType\" = 'Source' AND \"EntityId\" = @id",
                new NpgsqlParameter("id", id));
            if (embeddingCount > 0) break;
            await Task.Delay(2000);
        }

        Assert.True(embeddingCount > 0, "Embedding worker did not create a row within 30 seconds");
    }
}
