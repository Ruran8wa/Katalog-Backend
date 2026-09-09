using Katalog_Backend.DTO;
using Katalog_Backend.Models;

namespace Katalog_Backend.Mappers;

public static class VariantMapper
{
    public static VariantImageResponseDto ToDto(this VariantImage image)
    {
        return new VariantImageResponseDto
        {
            Id = image.Id,
            ImageUrl = image.ImageUrl,
            PublicId = image.PublicId,
            IsPrimary = image.IsPrimary,
            DisplayOrder = image.DisplayOrder
        };
    }

    public static VariantResponseDto ToDto(this Variant variant)
    {
        return new VariantResponseDto
        {
            Id = variant.Id,
            Name = variant.Name,
            PriceOverride = variant.PriceOverride,
            Sku = variant.Sku,
            Quantity = variant.Quantity,
            ProductId = variant.ProductId,
            ProductName = variant.Product?.Name,
            Images = variant.Images?.Select(img => img.ToDto()).OrderBy(img => img.DisplayOrder).ToList() ?? new()
        };
    }

    public static Variant ToEntity(this CreateVariantDto dto)
    {
        return new Variant
        {
            Name = dto.Name,
            PriceOverride = dto.PriceOverride,
            Sku = dto.Sku,
            Quantity = dto.Quantity,
            ProductId = dto.ProductId,
            Product = null!
        };
    }

    public static void UpdateEntity(this UpdateVariantDto dto, Variant variant)
    {
        variant.Name = dto.Name;
        variant.PriceOverride = dto.PriceOverride;
        variant.Sku = dto.Sku;
        variant.Quantity = dto.Quantity;
    }
}
