using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using PAOS.Tests.E2E.Helpers;

namespace PAOS.Tests.E2E.Commitments;

[Collection("E2E")]
public class CommitmentsE2ETests(E2EFixture f) : IAsyncLifetime
{
    public async Task InitializeAsync() =>
        await CleanupHelper.DeleteTablesAsync(f.Db,
            "AuditLogs", "CommitmentStatusHistories", "CommitmentSources", "Commitments", "SourceChunks", "Sources");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateCommitment_Returns201AndPersistsToDB()
    {
        var res = await f.Http.PostAsJsonAsync("/commitments", new
        {
            description = "Deliver API by end of month",
            confidence = 0.9
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "Commitments", "\"Id\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateCommitment_SeedsInitialStatusHistory()
    {
        var res = await f.Http.PostAsJsonAsync("/commitments", new
        {
            description = "Write tests by Friday",
            confidence = 1.0
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "CommitmentStatusHistories",
            "\"CommitmentId\" = @id AND \"Status\" = 'open'",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetCommitments_ReturnsList()
    {
        await f.Http.PostAsJsonAsync("/commitments", new { description = "First commitment", confidence = 1.0 });
        await f.Http.PostAsJsonAsync("/commitments", new { description = "Second commitment", confidence = 0.8 });

        var res = await f.Http.GetAsync("/commitments");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(list.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task GetCommitments_FiltersByStatus()
    {
        var res = await f.Http.PostAsJsonAsync("/commitments", new { description = "Open commitment", confidence = 1.0 });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        await f.Http.PutAsJsonAsync($"/commitments/{id}/status", new { status = "done", notes = "Completed" });

        var openRes = await f.Http.GetAsync("/commitments?status=open");
        var doneRes = await f.Http.GetAsync("/commitments?status=done");

        var openList = await openRes.Content.ReadFromJsonAsync<JsonElement>();
        var doneList = await doneRes.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, openList.GetArrayLength());
        Assert.Equal(1, doneList.GetArrayLength());
    }

    [Fact]
    public async Task UpdateStatus_AppendsStatusHistory()
    {
        var postRes = await f.Http.PostAsJsonAsync("/commitments", new
        {
            description = "Status history test",
            confidence = 1.0
        });
        var body = await postRes.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        await f.Http.PutAsJsonAsync($"/commitments/{id}/status", new { status = "in-progress", notes = "Started" });

        var count = await CleanupHelper.CountRowsAsync(f.Db, "CommitmentStatusHistories",
            "\"CommitmentId\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(2, count); // initial "open" + "in-progress"
    }

    [Fact]
    public async Task CreateCommitment_WithSourceIds_LinksJunctionRows()
    {
        // Create source first
        var sourceRes = await f.Http.PostAsJsonAsync("/sources", new
        {
            type = "note",
            rawContent = "Evidence for commitment",
            extractionMethod = "manual",
            confidence = 1.0
        });
        var sourceBody = await sourceRes.Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = sourceBody.GetProperty("id").GetString()!;

        var res = await f.Http.PostAsJsonAsync("/commitments", new
        {
            description = "Commitment with evidence",
            confidence = 1.0,
            sourceIds = new[] { sourceId }
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(body.GetProperty("id").GetString()!);

        var count = await CleanupHelper.CountRowsAsync(f.Db, "CommitmentSources",
            "\"CommitmentId\" = @id",
            new NpgsqlParameter("id", id));
        Assert.Equal(1, count);
    }
}
