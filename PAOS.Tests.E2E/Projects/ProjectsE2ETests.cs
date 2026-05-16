using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3.Model;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Projects;

[Collection("E2E")]
public class ProjectsE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "ProjectFiles", "ProjectStatusUpdates", "ProjectBlockers", "ProjectMembers", "ProjectEvents", "Projects");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateProject_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/projects", new
        {
            name = "Project Alpha",
            description = "First E2E test project"
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "Projects", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProject_ReturnsWithNavProperties()
    {
        var postRes = await f.Http.PostAsJsonAsync("/projects", new { name = "Project Beta" });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        var getRes = await f.Http.GetAsync($"/projects/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var project = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Project Beta", project.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, project.GetProperty("members").ValueKind);
        Assert.Equal(JsonValueKind.Array, project.GetProperty("blockers").ValueKind);
        Assert.Equal(JsonValueKind.Array, project.GetProperty("files").ValueKind);
    }

    [Fact]
    public async Task AddBlockerAndResolve_WorksEndToEnd()
    {
        var projectRes = await f.Http.PostAsJsonAsync("/projects", new { name = "Blocker Test Project" });
        var projectBody = await projectRes.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = projectBody.GetProperty("id").GetString()!;

        var blockerRes = await f.Http.PostAsJsonAsync($"/projects/{projectId}/blockers", new
        {
            description = "Waiting for infra access"
        });
        Assert.Equal(HttpStatusCode.Created, blockerRes.StatusCode);
        var blockerBody = await blockerRes.Content.ReadFromJsonAsync<JsonElement>();
        var blockerId = blockerBody.GetProperty("id").GetString()!;

        var resolveRes = await f.Http.PutAsync($"/projects/{projectId}/blockers/{blockerId}/resolve", null);
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        var resolved = await resolveRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.String, resolved.GetProperty("resolvedAt").ValueKind);
    }

    [Fact]
    public async Task PostStatusUpdate_Returns201()
    {
        var projectRes = await f.Http.PostAsJsonAsync("/projects", new { name = "Status Test Project" });
        var projectBody = await projectRes.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = projectBody.GetProperty("id").GetString()!;

        var statusRes = await f.Http.PostAsJsonAsync($"/projects/{projectId}/status", new
        {
            status = "in-progress",
            summary = "Development started"
        });
        Assert.Equal(HttpStatusCode.Created, statusRes.StatusCode);
    }

    [Fact]
    public async Task RegisterFile_PersistsMetadataToDB()
    {
        var projectRes = await f.Http.PostAsJsonAsync("/projects", new { name = "File Test Project" });
        var projectBody = await projectRes.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = projectBody.GetProperty("id").GetString()!;

        var fileKey = $"projects/{projectId}/readme.txt";
        var fileRes = await f.Http.PostAsJsonAsync($"/projects/{projectId}/files", new
        {
            fileKey,
            filename = "readme.txt"
        });
        Assert.Equal(HttpStatusCode.Created, fileRes.StatusCode);
        var fileBody = await fileRes.Content.ReadFromJsonAsync<JsonElement>();
        var fileId = Guid.Parse(fileBody.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "ProjectFiles", "\"Id\" = @id",
            new NpgsqlParameter("id", fileId));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MinioUpload_FileExistsInBucket()
    {
        var key = $"e2e-test/{Guid.NewGuid()}.txt";
        var content = "Hello from E2E test";

        // Upload directly to MinIO via S3 client
        await f.S3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = E2EFixture.MinioBucket,
            Key = key,
            ContentBody = content
        });

        // Verify it exists
        var meta = await f.S3.GetObjectMetadataAsync(E2EFixture.MinioBucket, key);
        Assert.Equal(System.Net.HttpStatusCode.OK, meta.HttpStatusCode);

        // Cleanup
        await f.S3.DeleteObjectAsync(E2EFixture.MinioBucket, key);
    }
}
