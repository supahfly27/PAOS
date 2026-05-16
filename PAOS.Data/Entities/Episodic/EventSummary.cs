namespace PAOS.Data.Entities.Episodic;

public class EventSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MemoryEvent Event { get; set; } = null!;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
