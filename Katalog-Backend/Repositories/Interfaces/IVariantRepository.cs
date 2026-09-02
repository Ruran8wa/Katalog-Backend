using Katalog_Backend.Models;

namespace Katalog_Backend.Repositories.Interfaces;

public interface IVariantRepository
{
    Task<IEnumerable<Variant>> GetAllAsync(int? productId = null);
    Task<Variant?> GetByIdAsync(int id);
    Task<Variant?> GetBySkuAsync(string sku);
    Task<Variant> CreateAsync(Variant variant);
    Task<Variant> UpdateAsync(Variant variant);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> SkuExistsAsync(string sku, int? excludeVariantId = null);
    Task<bool> ProductExistsAsync(int productId);

    // Image operations
    Task<VariantImage> AddImageAsync(VariantImage image);
    Task<VariantImage?> GetImageByIdAsync(int imageId);
    Task<bool> DeleteImageAsync(VariantImage image);
    Task SetPrimaryImageAsync(int variantId, int primaryImageId);
}
