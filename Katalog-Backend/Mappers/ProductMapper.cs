using Katalog_Backend.DTO;
using Katalog_Backend.Models;

namespace Katalog_Backend.Mappers;

public static class ProductMapper
{
    public static ProductResponseDto ToDto(this Product product)
    {
        return new ProductResponseDto(
            product.Id,
            product.Name,
            product.Description,
            product.BasePrice,
            product.Material,
            product.CategoryId,
            product.Category?.CategoryName
        );
    }

    public static Product ToEntity(this CreateProductDto dto)
    {
        return new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            BasePrice = dto.BasePrice,
            Material = dto.Material,
            CategoryId = dto.CategoryId,
            Category = null!,
            Variants = new List<Variant>()
        };
    }

    public static void UpdateEntity(this UpdateProductDto dto, Product product)
    {
        product.Name = dto.Name;
        product.Description = dto.Description;
        product.BasePrice = dto.BasePrice;
        product.Material = dto.Material;
        product.CategoryId = dto.CategoryId;
    }
}
