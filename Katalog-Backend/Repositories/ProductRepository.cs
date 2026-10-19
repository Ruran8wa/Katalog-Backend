using Katalog_Backend.Data;
using Katalog_Backend.DTO;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Repositories;

public class ProductRepository(ApplicationDbContext context) : IProductRepository
{
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(ProductQueryDto query)
    {
        var q = context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .AsQueryable();

        // --- Filters ---
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(p => p.Name.Contains(query.Search));

        if (query.CategoryId.HasValue)
            q = q.Where(p => p.CategoryId == query.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(query.Material))
            q = q.Where(p => p.Material.Contains(query.Material));

        if (query.MinPrice.HasValue)
            q = q.Where(p => p.BasePrice >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            q = q.Where(p => p.BasePrice <= query.MaxPrice.Value);

        // --- Total count (before paging) ---
        var totalCount = await q.CountAsync();

        // --- Sorting ---
        q = (query.SortBy.ToLower(), query.SortDirection.ToLower()) switch
        {
            ("name", "desc") => q.OrderByDescending(p => p.Name),
            ("name", _)      => q.OrderBy(p => p.Name),
            ("price", "desc")=> q.OrderByDescending(p => p.BasePrice),
            ("price", _)     => q.OrderBy(p => p.BasePrice),
            ("id", "desc")   => q.OrderByDescending(p => p.Id),
            _                => q.OrderBy(p => p.Id),
        };

        // --- Pagination ---
        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();
        
        // Reload category navigation property if present
        await context.Entry(product).Reference(p => p.Category).LoadAsync();
        return product;
    }

    public async Task<Product> UpdateAsync(Product product)
    {
        context.Products.Update(product);
        await context.SaveChangesAsync();
        await context.Entry(product).Reference(p => p.Category).LoadAsync();
        return product;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product == null)
        {
            return false;
        }

        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await context.Products.AnyAsync(p => p.Id == id);
    }
}

