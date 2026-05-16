namespace PAOS.Data.Entities.Decisions;

public class DecisionOutcome
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;
    public string OutcomeDescription { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
