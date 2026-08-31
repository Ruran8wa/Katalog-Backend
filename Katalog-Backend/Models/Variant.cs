namespace Katalog_Backend.Models;

public class Variant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? PriceOverride { get; set; }
    public required string SKU { get; set; }
    public int Quantity { get; set; }
    
    public int ProductId {get; set;}
    public Product Product { get; set; }
}