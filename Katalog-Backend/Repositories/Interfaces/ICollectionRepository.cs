using Katalog_Backend.Models;

namespace Katalog_Backend.Repositories.Interfaces;

public interface ICollectionRepository
{
    Task<IEnumerable<Collection>> GetAllAsync();
    Task<Collection?> GetByIdAsync(int id);
    Task<Collection?> GetByNameAsync(string name);
    Task<Collection> CreateAsync(Collection collection);
    Task<Collection> UpdateAsync(Collection collection);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> NameExistsAsync(string name, int? excludeCollectionId = null);

    // Product association
    Task<bool> ProductExistsAsync(int productId);
    Task<bool> IsProductInCollectionAsync(int collectionId, int productId);
    Task AddProductAsync(int collectionId, int productId);
    Task RemoveProductAsync(int collectionId, int productId);
}
