namespace PAOS.Data.Entities.Commitments;

public class CommitmentSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommitmentId { get; set; }
    public Commitment Commitment { get; set; } = null!;
    public Guid SourceId { get; set; }
}
