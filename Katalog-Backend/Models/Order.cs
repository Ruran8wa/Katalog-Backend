namespace Katalog_Backend.Models;

public class Order
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public int PriceAtPurchase { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public required ApplicationUser User { get; set; }
    public required Variant Variant { get; set; }
}