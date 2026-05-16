namespace PAOS.Data.Entities.Projects;

public class ProjectFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string FileKey { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
