using Katalog_Backend.DTO;

namespace Katalog_Backend.Repositories.Interfaces;

public interface ICategoryRepo
{
    Task<CategoryResponseDto> CreateCategory(CreateCategoryDto categoryDto);
    Task<List<CategoryResponseDto>> GetAllCategories();
    Task<CategoryResponseDto> GetCategoryById(int id);
    Task<List<CategoryResponseDto>> GetCategoriesByParentId(int parentId);
    Task DeleteCategory(int categoryId);
    Task<CategoryResponseDto> UpdateCategory(UpdateCategoryDto category);
    Task<bool> CategoryExists(int id);
}