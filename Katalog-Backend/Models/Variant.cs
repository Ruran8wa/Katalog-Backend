using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.Models;

public class Variant
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public int? PriceOverride { get; set; }
    [MaxLength(20)]
    public required string Sku { get; set; }
    public int Quantity { get; set; }
    
    public int ProductId {get; set;}
    public required Product Product { get; set; }
}