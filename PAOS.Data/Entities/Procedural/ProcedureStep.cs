namespace PAOS.Data.Entities.Procedural;

public class ProcedureStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcedureId { get; set; }
    public Procedure Procedure { get; set; } = null!;
    public int StepOrder { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
}
