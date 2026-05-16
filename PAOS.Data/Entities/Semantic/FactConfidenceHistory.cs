namespace PAOS.Data.Entities.Semantic;

public class FactConfidenceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FactId { get; set; }
    public Fact Fact { get; set; } = null!;
    public float Confidence { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
