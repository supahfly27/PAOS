using Pgvector;

namespace PAOS.Data.Entities.Search;

public class MemoryEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Vector Embedding { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
