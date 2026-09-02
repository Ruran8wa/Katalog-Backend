using Katalog_Backend.DTO;
using Katalog_Backend.Models;

namespace Katalog_Backend.Mappers;

public static class CategoryMapper
{
    public static CategoryResponseDto ToCategoryResponseDto(this Category category, HashSet<int>? visitedIds = null)
    {
        visitedIds ??= new HashSet<int>();

        if (visitedIds.Contains(category.Id))
        {
            return new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.CategoryName,
                CategoryParentId = category.CategoryParentId,
                Children = new List<CategoryResponseDto>()
            };
        }

        visitedIds.Add(category.Id);

        return new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.CategoryName,
            CategoryParentId = category.CategoryParentId,
            Children = category.Children != null && category.Children.Count > 0
                ? category.Children.Select(c => c.ToCategoryResponseDto(new HashSet<int>(visitedIds))).ToList()
                : new List<CategoryResponseDto>()
        };
    }

    public static Category ToCategoryFromCreateDto(this CreateCategoryDto dto)
    {
        return new Category
        {
            CategoryName = dto.Name,
            CategoryParentId = dto.CategoryParentId
        };
    }
}
