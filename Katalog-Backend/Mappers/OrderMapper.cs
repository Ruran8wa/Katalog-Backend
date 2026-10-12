using Katalog_Backend.DTO;
using Katalog_Backend.Models;

namespace Katalog_Backend.Mappers;

public static class OrderMapper
{
    public static OrderResponseDto ToDto(this Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            UserId = order.UserId,
            UserEmail = order.User?.Email,
            VariantId = order.VariantId,
            VariantName = order.Variant?.Name,
            Sku = order.Variant?.Sku,
            ProductId = order.Variant != null ? order.Variant.ProductId : (int?)null,
            ProductName = order.Variant?.Product?.Name,
            Quantity = order.Quantity,
            PriceAtPurchase = order.PriceAtPurchase,
            TotalPrice = order.Quantity * order.PriceAtPurchase,
            Status = order.Status,
            CreatedAt = order.CreatedAt
        };
    }

    public static Order ToEntity(this CreateOrderDto dto, string userId, int priceAtPurchase)
    {
        return new Order
        {
            UserId = userId,
            VariantId = dto.VariantId,
            Quantity = dto.Quantity,
            PriceAtPurchase = priceAtPurchase,
            CreatedAt = DateTime.UtcNow,
            User = null!,
            Variant = null!
        };
    }
}
