using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace PAOS.Data.Entities.Search;

public class MemoryEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Vector column added via raw SQL in InitialSchema migration; EF Core maps it in Phase 4
    [NotMapped]
    public Vector? Embedding { get; set; }
}
