namespace PAOS.Data.Entities.Episodic;

public class EventSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MemoryEvent Event { get; set; } = null!;
    public Guid SourceId { get; set; }
}
