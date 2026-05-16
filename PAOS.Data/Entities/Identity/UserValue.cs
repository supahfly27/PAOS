namespace PAOS.Data.Entities.Identity;

public class UserValue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string ValueName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
