namespace PAOS.Data.Entities.Search;

public class RetrievalFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SearchLogId { get; set; }
    public SearchLog SearchLog { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public bool WasHelpful { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
