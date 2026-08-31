namespace Katalog_Backend.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int BasePrice { get; set; }
    public string Material {get; set;}
    
    public int CategoryId { get; set; }
    
    public Category Category { get; set; }
    public ICollection<Variant> Variants { get; set; }
    public ICollection<CollectionProducts> CollectionProducts { get; set; } = new List<CollectionProducts>();

}