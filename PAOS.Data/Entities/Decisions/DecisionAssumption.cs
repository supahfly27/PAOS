namespace PAOS.Data.Entities.Decisions;

public class DecisionAssumption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;
    public string AssumptionText { get; set; } = string.Empty;
    public bool StillValid { get; set; } = true;
}
