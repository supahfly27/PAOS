namespace PAOS.Data.Entities.Decisions;

public class Decision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime MadeAt { get; set; }
    public DateTime? RevisitAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DecisionOption> Options { get; set; } = [];
    public ICollection<DecisionAssumption> Assumptions { get; set; } = [];
    public ICollection<DecisionOutcome> Outcomes { get; set; } = [];
}
