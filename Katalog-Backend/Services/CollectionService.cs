using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Mappers;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services.Interfaces;

namespace Katalog_Backend.Services;

public class CollectionService : ICollectionService
{
    private readonly ICollectionRepository _collectionRepository;

    public CollectionService(ICollectionRepository collectionRepository)
    {
        _collectionRepository = collectionRepository;
    }

    public async Task<IEnumerable<CollectionResponseDto>> GetAllCollectionsAsync()
    {
        var collections = await _collectionRepository.GetAllAsync();
        return collections.Select(c => c.ToDto());
    }

    public async Task<CollectionResponseDto> GetCollectionByIdAsync(int id)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        if (collection == null)
            throw new CollectionNotFoundException(id);

        return collection.ToDto();
    }

    public async Task<CollectionResponseDto> CreateCollectionAsync(CreateCollectionDto createDto)
    {
        if (string.IsNullOrWhiteSpace(createDto.Name))
            throw new ArgumentException("Collection name cannot be empty.");

        if (await _collectionRepository.NameExistsAsync(createDto.Name))
            throw new DuplicateCollectionNameException(createDto.Name);

        var collection = createDto.ToEntity();
        var created = await _collectionRepository.CreateAsync(collection);
        return created.ToDto();
    }

    public async Task<CollectionResponseDto> UpdateCollectionAsync(int id, UpdateCollectionDto updateDto)
    {
        var collection = await _collectionRepository.GetByIdAsync(id);
        if (collection == null)
            throw new CollectionNotFoundException(id);

        if (string.IsNullOrWhiteSpace(updateDto.Name))
            throw new ArgumentException("Collection name cannot be empty.");

        if (collection.Name != updateDto.Name && await _collectionRepository.NameExistsAsync(updateDto.Name, id))
            throw new DuplicateCollectionNameException(updateDto.Name);

        collection.Name = updateDto.Name;
        var updated = await _collectionRepository.UpdateAsync(collection);
        return updated.ToDto();
    }

    public async Task DeleteCollectionAsync(int id)
    {
        if (!await _collectionRepository.ExistsAsync(id))
            throw new CollectionNotFoundException(id);

        await _collectionRepository.DeleteAsync(id);
    }

    public async Task AddProductToCollectionAsync(int collectionId, int productId)
    {
        if (!await _collectionRepository.ExistsAsync(collectionId))
            throw new CollectionNotFoundException(collectionId);

        if (!await _collectionRepository.ProductExistsAsync(productId))
            throw new KeyNotFoundException($"Product with ID {productId} was not found.");

        if (await _collectionRepository.IsProductInCollectionAsync(collectionId, productId))
            throw new ProductAlreadyInCollectionException(productId, collectionId);

        await _collectionRepository.AddProductAsync(collectionId, productId);
    }

    public async Task RemoveProductFromCollectionAsync(int collectionId, int productId)
    {
        if (!await _collectionRepository.ExistsAsync(collectionId))
            throw new CollectionNotFoundException(collectionId);

        if (!await _collectionRepository.IsProductInCollectionAsync(collectionId, productId))
            throw new ProductNotInCollectionException(productId, collectionId);

        await _collectionRepository.RemoveProductAsync(collectionId, productId);
    }
}
