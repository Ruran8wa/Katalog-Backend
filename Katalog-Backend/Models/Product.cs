using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.Models;

public class Product
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
    public int BasePrice { get; set; }
    [MaxLength(100)]
    public string Material { get; set; } = string.Empty;
    
    public int CategoryId { get; set; }
    
    public required Category Category { get; set; }
    public required ICollection<Variant> Variants { get; set; }
    public ICollection<CollectionProduct> CollectionProducts { get; set; } = new List<CollectionProduct>();

}