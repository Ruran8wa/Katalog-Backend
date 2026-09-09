using Katalog_Backend.DTO;

namespace Katalog_Backend.Services.Interfaces;

public interface ICollectionService
{
    Task<IEnumerable<CollectionResponseDto>> GetAllCollectionsAsync();
    Task<CollectionResponseDto> GetCollectionByIdAsync(int id);
    Task<CollectionResponseDto> CreateCollectionAsync(CreateCollectionDto createDto);
    Task<CollectionResponseDto> UpdateCollectionAsync(int id, UpdateCollectionDto updateDto);
    Task DeleteCollectionAsync(int id);

    // Product associations
    Task AddProductToCollectionAsync(int collectionId, int productId);
    Task RemoveProductFromCollectionAsync(int collectionId, int productId);
}
