namespace PAOS.Data.Entities.Procedural;

public class ProcedureFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcedureRunId { get; set; }
    public ProcedureRun ProcedureRun { get; set; } = null!;
    public int Rating { get; set; }
    public string Notes { get; set; } = string.Empty;
}
