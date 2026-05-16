using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenAI.Embeddings;
using PAOS.Data;
using StackExchange.Redis;

namespace PAOS.MemoryWorker.Workers;

public class EmbeddingWorker(
    ILogger<EmbeddingWorker> logger,
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    IConfiguration config) : BackgroundService
{
    private const string QueueKey = "embed_queue";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EmbeddingWorker started");

        var apiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("OpenAI:ApiKey not configured — EmbeddingWorker will not process jobs");
            return;
        }

        var embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
        var db = redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var item = await db.ListLeftPopAsync(QueueKey);
                if (item.IsNullOrEmpty)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                var job = JsonSerializer.Deserialize<EmbedJob>((string)item!);
                if (job is null) continue;

                var response = await embeddingClient.GenerateEmbeddingAsync(job.Text, cancellationToken: stoppingToken);
                var floats = response.Value.ToFloats().ToArray();
                var vectorLiteral = "[" + string.Join(",", floats.Select(f => f.ToString("G7", CultureInfo.InvariantCulture))) + "]";

                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
                var cs = context.Database.GetConnectionString()!;

                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync(stoppingToken);
                await using var cmd = new NpgsqlCommand(
                    @"INSERT INTO ""MemoryEmbeddings"" (""Id"", ""EntityType"", ""EntityId"", ""CreatedAt"", ""Embedding"")
                      VALUES (@id, @entityType, @entityId, NOW(), @embedding::vector)
                      ON CONFLICT DO NOTHING", conn);
                cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("entityType", job.EntityType);
                cmd.Parameters.AddWithValue("entityId", Guid.Parse(job.EntityId));
                cmd.Parameters.AddWithValue("embedding", vectorLiteral);
                await cmd.ExecuteNonQueryAsync(stoppingToken);

                logger.LogInformation("Embedded {EntityType}/{EntityId}", job.EntityType, job.EntityId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing embed job");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

public record EmbedJob(string EntityType, string EntityId, string Text);
