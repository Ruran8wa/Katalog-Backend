using Katalog_Backend.DTO;
using Katalog_Backend.Models;

namespace Katalog_Backend.Mappers;

public static class CollectionMapper
{
    public static CollectionProductResponseDto ToCollectionProductDto(this CollectionProduct cp)
    {
        return new CollectionProductResponseDto
        {
            ProductId = cp.ProductId,
            ProductName = cp.Product?.Name ?? string.Empty,
            BasePrice = cp.Product?.BasePrice ?? 0,
            Material = cp.Product?.Material ?? string.Empty,
            CategoryId = cp.Product?.CategoryId ?? 0,
            CategoryName = cp.Product?.Category?.CategoryName
        };
    }

    public static CollectionResponseDto ToDto(this Collection collection)
    {
        return new CollectionResponseDto
        {
            Id = collection.Id,
            Name = collection.Name,
            Products = collection.CollectionProducts?
                .Select(cp => cp.ToCollectionProductDto())
                .ToList() ?? new()
        };
    }

    public static Collection ToEntity(this CreateCollectionDto dto)
    {
        return new Collection
        {
            Name = dto.Name
        };
    }
}
