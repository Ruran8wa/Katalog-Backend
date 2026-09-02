using Katalog_Backend.DTO;

namespace Katalog_Backend.Services.Interfaces;

public interface ICategoryService
{
    public Task<CategoryResponseDto> CreateCategory(CreateCategoryDto dto);
    public Task<CategoryResponseDto> UpdateCategory(UpdateCategoryDto dto);
    public Task<CategoryResponseDto> GetCategoryById(int id);
    public Task<List<CategoryResponseDto>> GetAllCategories();
    public Task DeleteCategory(int id);
    public Task<List<CategoryResponseDto>> GetCategoriesByParentId(int parentId);
}