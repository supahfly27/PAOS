namespace PAOS.Data.Entities.Episodic;

public class MemoryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<EventParticipant> Participants { get; set; } = [];
    public ICollection<EventSource> Sources { get; set; } = [];
    public ICollection<EventSummary> Summaries { get; set; } = [];
}
