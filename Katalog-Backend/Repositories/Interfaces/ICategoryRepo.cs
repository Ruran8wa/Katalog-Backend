using Katalog_Backend.DTO;
using Katalog_Backend.Models;

namespace Katalog_Backend.Repositories.Interfaces;

public interface ICategoryRepo
{
    public Task<CategoryResponseDto> CreateCategory(CreateCategoryDto categoryDto);
    public Task<List<CategoryResponseDto>> GetAllCategories();
    public Task<CategoryResponseDto> GetCategoryById(int id);
    public Task<List<CategoryResponseDto>> GetCategoriesByParentId(int parentId);
    public Task DeleteCategory(int categoryId);
    public Task UpdateCategory(UpdateCategoryDto category);
}