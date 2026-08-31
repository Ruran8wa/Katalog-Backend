namespace Katalog_Backend.Models;

public class Collection
{
    public int Id { get; set; }
    public string Name { get; set; }

    public ICollection<CollectionProducts> CollectionProducts { get; set; } = new List<CollectionProducts>();
}