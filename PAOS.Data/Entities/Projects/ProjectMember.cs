namespace PAOS.Data.Entities.Projects;

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid PersonId { get; set; }
    public string Role { get; set; } = string.Empty;
}
