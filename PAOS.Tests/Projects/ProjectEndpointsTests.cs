using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PAOS.Data;

namespace PAOS.Tests.Projects;

[Collection("Integration")]
public class ProjectEndpointsTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.ProjectFiles.RemoveRange(db.ProjectFiles);
        db.ProjectStatusUpdates.RemoveRange(db.ProjectStatusUpdates);
        db.ProjectBlockers.RemoveRange(db.ProjectBlockers);
        db.ProjectMembers.RemoveRange(db.ProjectMembers);
        db.ProjectEvents.RemoveRange(db.ProjectEvents);
        db.Projects.RemoveRange(db.Projects);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostProject_Returns201WithId()
    {
        var response = await _client.PostAsJsonAsync("/projects", new
        {
            name = "PAOS Memory Layer",
            description = "Building the memory subsystem"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task GetProject_ReturnsProjectWithCollections()
    {
        var created = await (await _client.PostAsJsonAsync("/projects", new { name = "Alpha", description = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/projects/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alpha", project.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, project.GetProperty("members").ValueKind);
        Assert.Equal(JsonValueKind.Array, project.GetProperty("blockers").ValueKind);
        Assert.Equal(JsonValueKind.Array, project.GetProperty("statusUpdates").ValueKind);
    }

    [Fact]
    public async Task PostBlocker_ThenResolve_ShowsResolvedAt()
    {
        var created = await (await _client.PostAsJsonAsync("/projects", new { name = "Beta", description = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = created.GetProperty("id").GetString()!;

        var blockerResponse = await _client.PostAsJsonAsync($"/projects/{projectId}/blockers", new
        {
            description = "Waiting for API keys from vendor"
        });
        Assert.Equal(HttpStatusCode.Created, blockerResponse.StatusCode);
        var blockerCreated = await blockerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var blockerId = blockerCreated.GetProperty("id").GetString()!;

        var resolveResponse = await _client.PutAsJsonAsync($"/projects/{projectId}/blockers/{blockerId}/resolve", new { });
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(JsonValueKind.Null, resolved.GetProperty("resolvedAt").ValueKind);

        var getResponse = await _client.GetAsync($"/projects/{projectId}");
        var project = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var blockers = project.GetProperty("blockers");
        Assert.Equal(1, blockers.GetArrayLength());
        Assert.NotEqual(JsonValueKind.Null, blockers[0].GetProperty("resolvedAt").ValueKind);
    }

    [Fact]
    public async Task PostStatus_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/projects", new { name = "Gamma", description = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var statusResponse = await _client.PostAsJsonAsync($"/projects/{id}/status", new
        {
            status = "on-track",
            summary = "All tasks completed ahead of schedule"
        });
        Assert.Equal(HttpStatusCode.Created, statusResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/projects/{id}");
        var project = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("on-track", project.GetProperty("status").GetString());
        var updates = project.GetProperty("statusUpdates");
        Assert.Equal(1, updates.GetArrayLength());
        Assert.Equal("All tasks completed ahead of schedule", updates[0].GetProperty("summary").GetString());
    }

    [Fact]
    public async Task PostFile_AppearsInGetResponse()
    {
        var created = await (await _client.PostAsJsonAsync("/projects", new { name = "Delta", description = (string?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        var fileResponse = await _client.PostAsJsonAsync($"/projects/{id}/files", new
        {
            fileKey = "projects/delta/spec.pdf",
            filename = "spec.pdf"
        });
        Assert.Equal(HttpStatusCode.Created, fileResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/projects/{id}");
        var project = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var files = project.GetProperty("files");
        Assert.Equal(1, files.GetArrayLength());
        Assert.Equal("spec.pdf", files[0].GetProperty("filename").GetString());
    }

    [Fact]
    public async Task GetProject_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
