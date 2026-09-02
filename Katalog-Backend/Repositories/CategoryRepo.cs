using Katalog_Backend.Data;
using Katalog_Backend.DTO;
using Katalog_Backend.Mappers;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Repositories;

public class CategoryRepo : ICategoryRepo
{
    private readonly ApplicationDbContext _context;
    
    public CategoryRepo(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryResponseDto> CreateCategory(CreateCategoryDto categoryDto)
    {
        var category = categoryDto.ToCategoryFromCreateDto();
        
        await _context.Categories.AddAsync(category);
        await _context.SaveChangesAsync();
        
        return category.ToCategoryResponseDto();
    }

    public async Task<List<CategoryResponseDto>> GetAllCategories()
    {
        var allCategories = await _context.Categories
            .AsNoTracking()
            .ToListAsync();
        return allCategories.Select(c => c.ToCategoryResponseDto()).ToList();
    }

    public async Task<CategoryResponseDto> GetCategoryById(int id)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        
        if (category == null)
            throw new KeyNotFoundException($"Category with id {id} was not found.");
        
        return category.ToCategoryResponseDto();
    }

    public async Task<List<CategoryResponseDto>> GetCategoriesByParentId(int parentId)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.CategoryParentId == parentId)
            .ToListAsync();

        return categories.Select(c => c.ToCategoryResponseDto()).ToList();
    }

    public async Task<CategoryResponseDto> UpdateCategory(UpdateCategoryDto category)
    {
        var categoryToBeUpdated = await _context.Categories.FirstOrDefaultAsync(c => c.Id == category.Id);
        if (categoryToBeUpdated == null)
            throw new KeyNotFoundException($"Category with id {category.Id} was not found.");

        categoryToBeUpdated.CategoryName = category.Name;
        await _context.SaveChangesAsync();

        return categoryToBeUpdated.ToCategoryResponseDto();
    }

    public async Task DeleteCategory(int categoryId)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);
        if (category == null)
            throw new KeyNotFoundException($"Category with id {categoryId} was not found.");
        
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> CategoryExists(int id)
    {
        return await _context.Categories.AnyAsync(c => c.Id == id);
    }
}