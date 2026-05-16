namespace PAOS.Data.Entities.People;

public class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Interaction> Interactions { get; set; } = [];
    public ICollection<PersonFact> Facts { get; set; } = [];
    public ICollection<Promise> Promises { get; set; } = [];
}
