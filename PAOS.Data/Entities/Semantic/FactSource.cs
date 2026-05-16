namespace PAOS.Data.Entities.Semantic;

public class FactSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FactId { get; set; }
    public Fact Fact { get; set; } = null!;
    public Guid SourceId { get; set; }
}
