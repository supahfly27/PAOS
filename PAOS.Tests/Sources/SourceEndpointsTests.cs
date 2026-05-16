using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Sources;

[Collection("Integration")]
public class SourceEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.SourceChunks.RemoveRange(db.SourceChunks);
        db.Sources.RemoveRange(db.Sources);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostSource_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/sources", new
        {
            type = "text",
            rawContent = "David committed to deliver the report by Friday",
            extractionMethod = "manual"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString();
        Assert.NotNull(id);
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public async Task PostSource_WritesAuditLogRow()
    {
        await _client.PostAsJsonAsync("/sources", new
        {
            type = "email",
            rawContent = "Meeting notes from Monday standup",
            extractionMethod = "email_parser"
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

        var audit = db.AuditLogs.Single();
        Assert.Equal("Source", audit.EntityType);
        Assert.Equal("created", audit.Action);
        Assert.Equal("api", audit.ChangedBy);
    }

    [Fact]
    public async Task GetSource_ReturnsStoredSource()
    {
        var postResponse = await _client.PostAsJsonAsync("/sources", new
        {
            type = "transcript",
            rawContent = "Alice: I will send the proposal by EOD.",
            extractionMethod = "auto"
        });
        var created = await postResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var getResponse = await _client.GetAsync($"/sources/{id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var source = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("transcript", source.GetProperty("type").GetString());
        Assert.Equal("Alice: I will send the proposal by EOD.", source.GetProperty("rawContent").GetString());
        Assert.Equal("auto", source.GetProperty("extractionMethod").GetString());
    }

    [Fact]
    public async Task GetSource_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/sources/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
