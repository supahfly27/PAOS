namespace PAOS.Data.Entities.Projects;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProjectMember> Members { get; set; } = [];
    public ICollection<ProjectEvent> Events { get; set; } = [];
    public ICollection<ProjectBlocker> Blockers { get; set; } = [];
    public ICollection<ProjectFile> Files { get; set; } = [];
    public ICollection<ProjectStatusUpdate> StatusUpdates { get; set; } = [];
}
