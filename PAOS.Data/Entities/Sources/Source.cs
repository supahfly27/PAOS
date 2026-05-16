namespace PAOS.Data.Entities.Sources;

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1.0f;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public ICollection<SourceChunk> Chunks { get; set; } = [];
}
