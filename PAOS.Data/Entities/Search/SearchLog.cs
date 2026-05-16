using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace PAOS.Data.Entities.Search;

public class SearchLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Query { get; set; } = string.Empty;
    public string ResultsJson { get; set; } = "[]";
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    public ICollection<RetrievalFeedback> Feedback { get; set; } = [];

    // Vector column added via raw SQL in InitialSchema migration; EF Core maps it in Phase 4
    [NotMapped]
    public Vector? QueryEmbedding { get; set; }
}
