namespace PAOS.Data.Entities.People;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string Notes { get; set; } = string.Empty;
}
