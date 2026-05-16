using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.People;

[Collection("E2E")]
public class PeopleE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "Promises", "PersonFacts", "Interactions", "People");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatePerson_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/people", new
        {
            name = "Alice Smith",
            email = "alice@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "People", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetPerson_ReturnsWithNavProperties()
    {
        var postRes = await f.Http.PostAsJsonAsync("/people", new { name = "Bob Jones" });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var getRes = await f.Http.GetAsync($"/people/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var person = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bob Jones", person.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, person.GetProperty("interactions").ValueKind);
        Assert.Equal(JsonValueKind.Array, person.GetProperty("facts").ValueKind);
        Assert.Equal(JsonValueKind.Array, person.GetProperty("promises").ValueKind);
    }

    [Fact]
    public async Task AddInteraction_Returns201AndAppearsInDB()
    {
        var personRes = await f.Http.PostAsJsonAsync("/people", new { name = "Carol White" });
        var personBody = await personRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = personBody.GetProperty("id").GetString()!;

        var intRes = await f.Http.PostAsJsonAsync($"/people/{id}/interactions", new
        {
            channel = "email",
            summary = "Discussed project timeline"
        });
        Assert.Equal(HttpStatusCode.Created, intRes.StatusCode);

        var intBody = await intRes.Content.ReadFromJsonAsync<JsonElement>();
        var intId = Guid.Parse(intBody.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "Interactions", "\"Id\" = @id",
            new NpgsqlParameter("id", intId));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddPersonFact_Returns201()
    {
        var personRes = await f.Http.PostAsJsonAsync("/people", new { name = "Dan Brown" });
        var personBody = await personRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = personBody.GetProperty("id").GetString()!;

        var factRes = await f.Http.PostAsJsonAsync($"/people/{id}/facts", new
        {
            fact = "Prefers async communication",
            confidence = 0.9
        });
        Assert.Equal(HttpStatusCode.Created, factRes.StatusCode);
    }

    [Fact]
    public async Task AddAndUpdatePromise_StatusChanges()
    {
        var personRes = await f.Http.PostAsJsonAsync("/people", new { name = "Eve Green" });
        var personBody = await personRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = personBody.GetProperty("id").GetString()!;

        var promiseRes = await f.Http.PostAsJsonAsync($"/people/{id}/promises", new
        {
            description = "Send follow-up email"
        });
        Assert.Equal(HttpStatusCode.Created, promiseRes.StatusCode);
        var promiseBody = await promiseRes.Content.ReadFromJsonAsync<JsonElement>();
        var promiseId = promiseBody.GetProperty("id").GetString()!;

        var updateRes = await f.Http.PutAsJsonAsync($"/people/{id}/promises/{promiseId}", new { status = "fulfilled" });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("fulfilled", updated.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetPromises_ReturnsList()
    {
        var personRes = await f.Http.PostAsJsonAsync("/people", new { name = "Frank Black" });
        var personBody = await personRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = personBody.GetProperty("id").GetString()!;

        await f.Http.PostAsJsonAsync($"/people/{id}/promises", new { description = "Call back tomorrow" });
        await f.Http.PostAsJsonAsync($"/people/{id}/promises", new { description = "Review document" });

        var listRes = await f.Http.GetAsync($"/people/{id}/promises");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);

        var promises = await listRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, promises.GetArrayLength());
    }
}
