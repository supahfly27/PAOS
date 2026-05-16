namespace PAOS.Data.Entities.Procedural;

public class Procedure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProcedureStep> Steps { get; set; } = [];
    public ICollection<ProcedureRun> Runs { get; set; } = [];
}
