using Katalog_Backend.Data;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly ApplicationDbContext _context;

    public CollectionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Collection>> GetAllAsync()
    {
        return await _context.Collections
            .Include(c => c.CollectionProducts)
                .ThenInclude(cp => cp.Product)
                    .ThenInclude(p => p.Category)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Collection?> GetByIdAsync(int id)
    {
        return await _context.Collections
            .Include(c => c.CollectionProducts)
                .ThenInclude(cp => cp.Product)
                    .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Collection?> GetByNameAsync(string name)
    {
        return await _context.Collections
            .FirstOrDefaultAsync(c => c.Name == name);
    }

    public async Task<Collection> CreateAsync(Collection collection)
    {
        await _context.Collections.AddAsync(collection);
        await _context.SaveChangesAsync();
        return collection;
    }

    public async Task<Collection> UpdateAsync(Collection collection)
    {
        _context.Collections.Update(collection);
        await _context.SaveChangesAsync();
        return collection;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var collection = await _context.Collections.FindAsync(id);
        if (collection == null) return false;

        _context.Collections.Remove(collection);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Collections.AnyAsync(c => c.Id == id);
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeCollectionId = null)
    {
        var query = _context.Collections.AsQueryable();
        if (excludeCollectionId.HasValue)
            query = query.Where(c => c.Id != excludeCollectionId.Value);

        return await query.AnyAsync(c => c.Name == name);
    }

    public async Task<bool> ProductExistsAsync(int productId)
    {
        return await _context.Products.AnyAsync(p => p.Id == productId);
    }

    public async Task<bool> IsProductInCollectionAsync(int collectionId, int productId)
    {
        return await _context.CollectionProducts
            .AnyAsync(cp => cp.CollectionId == collectionId && cp.ProductId == productId);
    }

    public async Task AddProductAsync(int collectionId, int productId)
    {
        var collectionProduct = new CollectionProduct
        {
            CollectionId = collectionId,
            ProductId = productId,
            Collection = null!,
            Product = null!
        };
        await _context.CollectionProducts.AddAsync(collectionProduct);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveProductAsync(int collectionId, int productId)
    {
        var entry = await _context.CollectionProducts
            .FirstOrDefaultAsync(cp => cp.CollectionId == collectionId && cp.ProductId == productId);

        if (entry != null)
        {
            _context.CollectionProducts.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }
}
