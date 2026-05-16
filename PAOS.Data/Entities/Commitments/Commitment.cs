namespace PAOS.Data.Entities.Commitments;

public class Commitment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "open";
    public float Confidence { get; set; } = 1.0f;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CommitmentSource> Sources { get; set; } = [];
    public ICollection<CommitmentStatusHistory> StatusHistory { get; set; } = [];
}
