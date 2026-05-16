using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenAI.Embeddings;
using PAOS.Data;
using PAOS.Data.Entities.Sources;

namespace PAOS.MemoryAPI.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/search");

        group.MapPost("/", async (SemanticSearchRequest req, MemoryDbContext db, IConfiguration config) =>
        {
            var apiKey = config["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                return Results.Problem("OpenAI:ApiKey not configured", statusCode: 503);

            var embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
            var response = await embeddingClient.GenerateEmbeddingAsync(req.Query);
            var floats = response.Value.ToFloats().ToArray();
            var vectorLiteral = "[" + string.Join(",", floats.Select(f => f.ToString("G7", CultureInfo.InvariantCulture))) + "]";

            var limit = req.Limit ?? 10;

            await using var conn = new NpgsqlConnection(db.Database.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT ""EntityType"", ""EntityId"",
                         1 - (""Embedding"" <=> @queryVec::vector) AS score
                  FROM ""MemoryEmbeddings""
                  ORDER BY ""Embedding"" <=> @queryVec::vector
                  LIMIT @limit", conn);
            cmd.Parameters.AddWithValue("queryVec", vectorLiteral);
            cmd.Parameters.AddWithValue("limit", limit);

            await using var reader = await cmd.ExecuteReaderAsync();
            var hits = new List<object>();
            while (await reader.ReadAsync())
            {
                hits.Add(new
                {
                    entityType = reader.GetString(0),
                    entityId = reader.GetGuid(1),
                    score = reader.GetDouble(2)
                });
            }

            return Results.Ok(hits);
        });

        group.MapPost("/keyword", async (KeywordSearchRequest req, MemoryDbContext db) =>
        {
            var limit = req.Limit ?? 10;
            var results = await db.Sources
                .Where(s => EF.Functions.ILike(s.RawContent, $"%{req.Query}%"))
                .Take(limit)
                .Select(s => new { s.Id, s.Type, s.RawContent })
                .ToListAsync();

            return Results.Ok(results);
        });
    }
}

public record SemanticSearchRequest(string Query, int? Limit = null);

public record KeywordSearchRequest(string Query, int? Limit = null);
