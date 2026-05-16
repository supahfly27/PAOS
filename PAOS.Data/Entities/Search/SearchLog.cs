using Pgvector;

namespace PAOS.Data.Entities.Search;

public class SearchLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Query { get; set; } = string.Empty;
    public Vector QueryEmbedding { get; set; } = null!;
    public string ResultsJson { get; set; } = "[]";
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RetrievalFeedback> Feedback { get; set; } = [];
}
