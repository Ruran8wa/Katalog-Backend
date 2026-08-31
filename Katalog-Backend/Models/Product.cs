namespace Katalog_Backend.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int BasePrice { get; set; }
    public string Material { get; set; } = string.Empty;
    
    public int CategoryId { get; set; }
    
    public Category Category { get; set; }
    public ICollection<Variant> Variants { get; set; }
    public ICollection<CollectionProduct> CollectionProducts { get; set; } = new List<CollectionProduct>();

}