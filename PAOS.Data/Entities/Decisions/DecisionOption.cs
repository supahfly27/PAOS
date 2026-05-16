namespace PAOS.Data.Entities.Decisions;

public class DecisionOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DecisionId { get; set; }
    public Decision Decision { get; set; } = null!;
    public string OptionText { get; set; } = string.Empty;
    public bool WasChosen { get; set; }
}
