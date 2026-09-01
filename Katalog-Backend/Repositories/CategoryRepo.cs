using Katalog_Backend.Data;
using Katalog_Backend.DTO;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Repositories;

public class CategoryRepo : ICategoryRepo
{
    private ApplicationDbContext _context;
    
    public CategoryRepo(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryResponseDto> CreateCategory(CreateCategoryDto category)
    {
        var newCategory = new Category
        {
            CategoryName = category.Name,
            CategoryParentId = category.CategoryParentId,
        };
        
        await _context.Categories.AddAsync(newCategory);
        await _context.SaveChangesAsync();
        
        return new CategoryResponseDto
        {
            Id = newCategory.Id,
            Name = newCategory.CategoryName,
            CategoryParentId = newCategory.CategoryParentId,
            Children = new List<CategoryResponseDto>()
                
        };
    }

    public async Task<List<CategoryResponseDto>> GetAllCategories()
    {
        var allCategories =  await _context.Categories.ToListAsync();
        return
        [
            .. allCategories.Select(c => new CategoryResponseDto
            {
                Name = c.CategoryName,
                CategoryParentId = c.CategoryParentId,
                Children = []
            })
        ];
    }

    public async Task <CategoryResponseDto> GetCategoryById(int id)
    {
        var categoryById = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
        
        if (categoryById == null)
            throw new KeyNotFoundException($"Category with id {id} was not found.");
        
        return new CategoryResponseDto
        {
            Id =  categoryById.Id,
            Name = categoryById.CategoryName,
            CategoryParentId = categoryById.CategoryParentId,
            Children = new List<CategoryResponseDto>()
        };
    }

    public Task<List<CategoryResponseDto>> GetCategoriesByParentId(int parentId)
    {
        throw new NotImplementedException();
    }

    public async Task<CategoryResponseDto> UpdateCategory(UpdateCategoryDto category)
    {

        var categoryToBeUpdated = await _context.Categories.FirstOrDefaultAsync(c => c.Id == category.Id);
        if(categoryToBeUpdated == null)
            throw new  KeyNotFoundException($"Category with name {category.Name} was not found.");

        categoryToBeUpdated.CategoryName = category.Name;
        await _context.SaveChangesAsync();

        return new CategoryResponseDto
        {
            Id = categoryToBeUpdated.Id,
            Name = categoryToBeUpdated.CategoryName,
            CategoryParentId = categoryToBeUpdated.CategoryParentId,
            Children = new List<CategoryResponseDto>()
        };
    }

    public async Task DeleteCategory(int categoryId)
    {
        var categoryWithId = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);
        if (categoryWithId == null)
            throw new KeyNotFoundException($"Category with id {categoryId} was not found.");
        
        _context.Categories.Remove(categoryWithId);
        await _context.SaveChangesAsync();
    }
}