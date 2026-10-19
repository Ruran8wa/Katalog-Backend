using Katalog_Backend.Data;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Repositories;

public class VariantRepository : IVariantRepository
{
    private readonly ApplicationDbContext _context;

    public VariantRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Variant>> GetAllAsync(int? productId = null)
    {
        var query = _context.Variants
            .Include(v => v.Product)
            .Include(v => v.Images)
            .AsNoTracking();

        if (productId.HasValue)
        {
            query = query.Where(v => v.ProductId == productId.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Variant?> GetByIdAsync(int id)
    {
        return await _context.Variants
            .Include(v => v.Product)
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Variant?> GetBySkuAsync(string sku)
    {
        return await _context.Variants
            .Include(v => v.Product)
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Sku == sku);
    }

    public async Task<Variant> CreateAsync(Variant variant)
    {
        await _context.Variants.AddAsync(variant);
        await _context.SaveChangesAsync();
        
        await _context.Entry(variant).Reference(v => v.Product).LoadAsync();
        return variant;
    }

    public async Task<Variant> UpdateAsync(Variant variant)
    {
        _context.Variants.Update(variant);
        await _context.SaveChangesAsync();

        await _context.Entry(variant).Reference(v => v.Product).LoadAsync();
        await _context.Entry(variant).Collection(v => v.Images).LoadAsync();
        return variant;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var variant = await _context.Variants.FindAsync(id);
        if (variant == null) return false;

        _context.Variants.Remove(variant);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Variants.AnyAsync(v => v.Id == id);
    }

    public async Task<bool> SkuExistsAsync(string sku, int? excludeVariantId = null)
    {
        var query = _context.Variants.AsQueryable();
        if (excludeVariantId.HasValue)
        {
            query = query.Where(v => v.Id != excludeVariantId.Value);
        }
        return await query.AnyAsync(v => v.Sku == sku);
    }

    public async Task<bool> ProductExistsAsync(int productId)
    {
        return await _context.Products.AnyAsync(p => p.Id == productId);
    }

    public async Task<VariantImage> AddImageAsync(VariantImage image)
    {
        await _context.VariantImages.AddAsync(image);
        await _context.SaveChangesAsync();
        return image;
    }

    public async Task<VariantImage?> GetImageByIdAsync(int imageId)
    {
        return await _context.VariantImages
            .Include(vi => vi.Variant)
            .FirstOrDefaultAsync(vi => vi.Id == imageId);
    }

    public async Task<bool> DeleteImageAsync(VariantImage image)
    {
        _context.VariantImages.Remove(image);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task SetPrimaryImageAsync(int variantId, int primaryImageId)
    {
        var images = await _context.VariantImages
            .Where(vi => vi.VariantId == variantId)
            .ToListAsync();

        foreach (var img in images)
        {
            img.IsPrimary = (img.Id == primaryImageId);
        }

        await _context.SaveChangesAsync();
    }
}
