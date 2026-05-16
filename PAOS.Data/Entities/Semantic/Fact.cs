namespace PAOS.Data.Entities.Semantic;

public class Fact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1.0f;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<FactSource> Sources { get; set; } = [];
}
