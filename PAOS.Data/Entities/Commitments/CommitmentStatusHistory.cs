namespace PAOS.Data.Entities.Commitments;

public class CommitmentStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommitmentId { get; set; }
    public Commitment Commitment { get; set; } = null!;
    public string Status { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}
