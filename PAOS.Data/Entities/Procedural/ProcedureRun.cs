namespace PAOS.Data.Entities.Procedural;

public class ProcedureRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcedureId { get; set; }
    public Procedure Procedure { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running";
    public ICollection<ProcedureFeedback> Feedback { get; set; } = [];
}
