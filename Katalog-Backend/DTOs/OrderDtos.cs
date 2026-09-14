using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.DTO;

public class CreateOrderDto
{
    public string? UserId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "VariantId must be a positive integer.")]
    public int VariantId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }
}

public class UpdateOrderQuantityDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public int VariantId { get; set; }
    public string? VariantName { get; set; }
    public string? Sku { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public int PriceAtPurchase { get; set; }
    public int TotalPrice { get; set; }
    public DateTime CreatedAt { get; set; }
}
