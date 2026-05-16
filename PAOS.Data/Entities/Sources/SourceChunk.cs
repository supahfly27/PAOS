namespace PAOS.Data.Entities.Sources;

public class SourceChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
}
