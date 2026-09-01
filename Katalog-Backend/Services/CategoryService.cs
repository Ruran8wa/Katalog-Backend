using Katalog_Backend.DTO;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services.Interfaces;

namespace Katalog_Backend.Services;

public class CategoryService(ICategoryRepo categoryRepo) : ICategoryService
{
    private ICategoryRepo _categoryRepo = categoryRepo;

    public Task<CategoryResponseDto> CreateCategory(CreateCategoryDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<List<CategoryResponseDto>> GetAllCategories()
    {
        throw new NotImplementedException();
    }

    public Task<CategoryResponseDto> GetCategoryById(int id)
    {
        throw new NotImplementedException();
    }

    public Task<List<CategoryResponseDto>> GetCategoriesByParentId(int parentId)
    {
        throw new NotImplementedException();
    }

    public Task<CategoryResponseDto> UpdateCategory(UpdateCategoryDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<CategoryResponseDto> DeleteCategory(int id)
    {
        throw new NotImplementedException();
    }
}