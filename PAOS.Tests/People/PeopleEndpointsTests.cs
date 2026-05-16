using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.People;

[Collection("Integration")]
public class PeopleEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.Promises.RemoveRange(db.Promises);
        db.PersonFacts.RemoveRange(db.PersonFacts);
        db.Interactions.RemoveRange(db.Interactions);
        db.People.RemoveRange(db.People);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostPerson_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/people", new
        {
            name = "Alice",
            email = "alice@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task GetPerson_ReturnsPersonWithCollections()
    {
        var created = await (await _client.PostAsJsonAsync("/people", new { name = "Bob", email = (string?)null, phone = (string?)null, notes = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/people/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bob", person.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, person.GetProperty("interactions").ValueKind);
        Assert.Equal(JsonValueKind.Array, person.GetProperty("facts").ValueKind);
        Assert.Equal(JsonValueKind.Array, person.GetProperty("promises").ValueKind);
    }

    [Fact]
    public async Task PostInteraction_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/people", new { name = "Carol", email = (string?)null, phone = (string?)null, notes = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var interactionResponse = await _client.PostAsJsonAsync($"/people/{id}/interactions", new
        {
            channel = "email",
            summary = "Discussed Q2 report deadlines"
        });
        Assert.Equal(HttpStatusCode.Created, interactionResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/people/{id}");
        var person = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var interactions = person.GetProperty("interactions");
        Assert.Equal(1, interactions.GetArrayLength());
        Assert.Equal("email", interactions[0].GetProperty("channel").GetString());
    }

    [Fact]
    public async Task PostPromise_ListOpenPromises_UpdateStatus()
    {
        var created = await (await _client.PostAsJsonAsync("/people", new { name = "Dave", email = (string?)null, phone = (string?)null, notes = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var personId = created.GetProperty("id").GetString()!;

        var promiseResponse = await _client.PostAsJsonAsync($"/people/{personId}/promises", new
        {
            description = "Send the budget report by Monday"
        });
        Assert.Equal(HttpStatusCode.Created, promiseResponse.StatusCode);
        var promiseCreated = await promiseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var promiseId = promiseCreated.GetProperty("id").GetString()!;

        var listResponse = await _client.GetAsync($"/people/{personId}/promises?status=open");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var promises = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, promises.GetArrayLength());
        Assert.Equal("Send the budget report by Monday", promises[0].GetProperty("description").GetString());

        var updateResponse = await _client.PutAsJsonAsync($"/people/{personId}/promises/{promiseId}", new { status = "fulfilled" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var emptyList = await (await _client.GetAsync($"/people/{personId}/promises?status=open"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, emptyList.GetArrayLength());
    }

    [Fact]
    public async Task PostFact_WithSourceId_Stored()
    {
        var personCreated = await (await _client.PostAsJsonAsync("/people", new { name = "Eve", email = (string?)null, phone = (string?)null, notes = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var personId = personCreated.GetProperty("id").GetString()!;

        var sourceCreated = await (await _client.PostAsJsonAsync("/sources", new { type = "text", rawContent = "Eve is a senior engineer", extractionMethod = "manual" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = sourceCreated.GetProperty("id").GetString()!;

        var factResponse = await _client.PostAsJsonAsync($"/people/{personId}/facts", new
        {
            fact = "Is a senior engineer",
            confidence = 0.9f,
            sourceId
        });
        Assert.Equal(HttpStatusCode.Created, factResponse.StatusCode);
    }

    [Fact]
    public async Task GetPerson_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/people/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
