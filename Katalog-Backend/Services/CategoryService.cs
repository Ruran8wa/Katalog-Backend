using Katalog_Backend.DTO;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services.Interfaces;

namespace Katalog_Backend.Services;

public class CategoryService(ICategoryRepo categoryRepo) : ICategoryService
{
    private readonly ICategoryRepo _categoryRepo = categoryRepo;

    public async Task<CategoryResponseDto> CreateCategory(CreateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Category name is required.");

        if (dto.CategoryParentId.HasValue)
        {
            var parentExists = await _categoryRepo.CategoryExists(dto.CategoryParentId.Value);
            if (!parentExists)
            {
                throw new KeyNotFoundException($"Parent category with id {dto.CategoryParentId.Value} was not found.");
            }
        }

        return await _categoryRepo.CreateCategory(dto);
    }

    public async Task<List<CategoryResponseDto>> GetAllCategories()
    {
        return await _categoryRepo.GetAllCategories();
    }

    public async Task<CategoryResponseDto> GetCategoryById(int id)
    {
        return await _categoryRepo.GetCategoryById(id);
    }

    public async Task<List<CategoryResponseDto>> GetCategoriesByParentId(int parentId)
    {
        var parentExists = await _categoryRepo.CategoryExists(parentId);
        if (!parentExists)
        {
            throw new KeyNotFoundException($"Parent category with id {parentId} was not found.");
        }

        return await _categoryRepo.GetCategoriesByParentId(parentId);
    }

    public async Task<CategoryResponseDto> UpdateCategory(UpdateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Category name is required.");

        return await _categoryRepo.UpdateCategory(dto);
    }

    public async Task DeleteCategory(int id)
    {
        await _categoryRepo.DeleteCategory(id);
    }
}