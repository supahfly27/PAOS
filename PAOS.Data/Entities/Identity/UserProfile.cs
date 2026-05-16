namespace PAOS.Data.Entities.Identity;

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public string CommunicationStyle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserPreference> Preferences { get; set; } = [];
    public ICollection<UserGoal> Goals { get; set; } = [];
    public ICollection<UserValue> Values { get; set; } = [];
    public ICollection<UserHabit> Habits { get; set; } = [];
}
