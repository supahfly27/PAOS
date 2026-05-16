namespace PAOS.Data.Entities.Semantic;

public class FactConflict
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FactIdA { get; set; }
    public Guid FactIdB { get; set; }
    public string ConflictType { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
