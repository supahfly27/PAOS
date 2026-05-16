namespace PAOS.Data.Entities.People;

public class Relationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string RelationshipType { get; set; } = string.Empty;
    public int Strength { get; set; }
    public string Notes { get; set; } = string.Empty;
}
