namespace PAOS.Data.Entities.People;

public class PersonFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Fact { get; set; } = string.Empty;
    public float Confidence { get; set; } = 1.0f;
    public Guid? SourceId { get; set; }
}
