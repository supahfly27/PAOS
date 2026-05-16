namespace PAOS.Data.Entities.Episodic;

public class EventParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MemoryEvent Event { get; set; } = null!;
    public Guid PersonId { get; set; }
    public string Role { get; set; } = string.Empty;
}
