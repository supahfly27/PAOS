namespace PAOS.Data.Entities.Identity;

public class UserHabit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string Habit { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
}
