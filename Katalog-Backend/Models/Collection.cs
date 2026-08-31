namespace Katalog_Backend.Models;

public class Collection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<CollectionProduct> CollectionProducts { get; set; } = new List<CollectionProduct>();
}