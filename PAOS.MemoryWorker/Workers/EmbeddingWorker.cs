namespace PAOS.MemoryWorker.Workers;

public class EmbeddingWorker(ILogger<EmbeddingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EmbeddingWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            // Phase 4: poll Redis queue and generate embeddings
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
