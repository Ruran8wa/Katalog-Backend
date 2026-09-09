using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services;
using Moq;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Services;

[TestFixture]
public class CollectionServiceTests
{
    private Mock<ICollectionRepository> _collectionRepositoryMock;
    private CollectionService _collectionService;

    [SetUp]
    public void Setup()
    {
        _collectionRepositoryMock = new Mock<ICollectionRepository>();
        _collectionService = new CollectionService(_collectionRepositoryMock.Object);
    }

    private static Collection CreateDummyCollection(int id = 1, string name = "Summer Collection")
    {
        return new Collection
        {
            Id = id,
            Name = name,
            CollectionProducts = new List<CollectionProduct>()
        };
    }

    // --- GetAllCollectionsAsync ---

    [Test]
    public async Task GetAllCollectionsAsync_ReturnsMappedDtoList()
    {
        // Arrange
        var collections = new List<Collection>
        {
            CreateDummyCollection(1, "Summer Collection"),
            CreateDummyCollection(2, "Best Sellers")
        };
        _collectionRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(collections);

        // Act
        var result = await _collectionService.GetAllCollectionsAsync();

        // Assert
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0].Name, Is.EqualTo("Summer Collection"));
        Assert.That(list[1].Name, Is.EqualTo("Best Sellers"));
    }

    // --- GetCollectionByIdAsync ---

    [Test]
    public async Task GetCollectionByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        var collection = CreateDummyCollection(5, "Featured");
        _collectionRepositoryMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(collection);

        // Act
        var result = await _collectionService.GetCollectionByIdAsync(5);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(5));
        Assert.That(result.Name, Is.EqualTo("Featured"));
    }

    [Test]
    public async Task GetCollectionByIdAsync_NonExistingId_ThrowsCollectionNotFoundException()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Collection?)null);

        // Act & Assert
        var ex = Assert.ThrowsAsync<CollectionNotFoundException>(
            async () => await _collectionService.GetCollectionByIdAsync(99));
        Assert.That(ex!.Message, Contains.Substring("99"));
    }

    // --- CreateCollectionAsync ---

    [Test]
    public async Task CreateCollectionAsync_ValidDto_CreatesAndReturnsDto()
    {
        // Arrange
        var createDto = new CreateCollectionDto { Name = "New Collection" };
        var created = CreateDummyCollection(10, "New Collection");

        _collectionRepositoryMock.Setup(r => r.NameExistsAsync("New Collection", null)).ReturnsAsync(false);
        _collectionRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Collection>())).ReturnsAsync(created);

        // Act
        var result = await _collectionService.CreateCollectionAsync(createDto);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(10));
        Assert.That(result.Name, Is.EqualTo("New Collection"));
        _collectionRepositoryMock.Verify(r => r.CreateAsync(It.Is<Collection>(c => c.Name == "New Collection")), Times.Once);
    }

    [Test]
    public async Task CreateCollectionAsync_DuplicateName_ThrowsDuplicateCollectionNameException()
    {
        // Arrange
        var createDto = new CreateCollectionDto { Name = "Existing" };
        _collectionRepositoryMock.Setup(r => r.NameExistsAsync("Existing", null)).ReturnsAsync(true);

        // Act & Assert
        Assert.ThrowsAsync<DuplicateCollectionNameException>(
            async () => await _collectionService.CreateCollectionAsync(createDto));
        _collectionRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Collection>()), Times.Never);
    }

    [Test]
    public async Task CreateCollectionAsync_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var createDto = new CreateCollectionDto { Name = "" };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(
            async () => await _collectionService.CreateCollectionAsync(createDto));
    }

    // --- UpdateCollectionAsync ---

    [Test]
    public async Task UpdateCollectionAsync_ValidDto_UpdatesAndReturnsDto()
    {
        // Arrange
        var collection = CreateDummyCollection(1, "Old Name");
        var updateDto = new UpdateCollectionDto { Name = "New Name" };

        _collectionRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(collection);
        _collectionRepositoryMock.Setup(r => r.NameExistsAsync("New Name", 1)).ReturnsAsync(false);
        _collectionRepositoryMock.Setup(r => r.UpdateAsync(collection)).ReturnsAsync(collection);

        // Act
        var result = await _collectionService.UpdateCollectionAsync(1, updateDto);

        // Assert
        Assert.That(result.Name, Is.EqualTo("New Name"));
        _collectionRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Collection>(c => c.Name == "New Name")), Times.Once);
    }

    [Test]
    public async Task UpdateCollectionAsync_NonExistingId_ThrowsCollectionNotFoundException()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Collection?)null);

        // Act & Assert
        Assert.ThrowsAsync<CollectionNotFoundException>(
            async () => await _collectionService.UpdateCollectionAsync(99, new UpdateCollectionDto { Name = "X" }));
    }

    [Test]
    public async Task UpdateCollectionAsync_DuplicateName_ThrowsDuplicateCollectionNameException()
    {
        // Arrange
        var collection = CreateDummyCollection(1, "Old Name");
        _collectionRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(collection);
        _collectionRepositoryMock.Setup(r => r.NameExistsAsync("Taken Name", 1)).ReturnsAsync(true);

        // Act & Assert
        Assert.ThrowsAsync<DuplicateCollectionNameException>(
            async () => await _collectionService.UpdateCollectionAsync(1, new UpdateCollectionDto { Name = "Taken Name" }));
    }

    // --- DeleteCollectionAsync ---

    [Test]
    public async Task DeleteCollectionAsync_ExistingId_Deletes()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _collectionService.DeleteCollectionAsync(1));
        _collectionRepositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Test]
    public async Task DeleteCollectionAsync_NonExistingId_ThrowsCollectionNotFoundException()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.ExistsAsync(99)).ReturnsAsync(false);

        // Act & Assert
        Assert.ThrowsAsync<CollectionNotFoundException>(
            async () => await _collectionService.DeleteCollectionAsync(99));
        _collectionRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    // --- AddProductToCollectionAsync ---

    [Test]
    public async Task AddProductToCollectionAsync_ValidIds_AddsProduct()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.ProductExistsAsync(5)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.IsProductInCollectionAsync(1, 5)).ReturnsAsync(false);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _collectionService.AddProductToCollectionAsync(1, 5));
        _collectionRepositoryMock.Verify(r => r.AddProductAsync(1, 5), Times.Once);
    }

    [Test]
    public async Task AddProductToCollectionAsync_ProductAlreadyInCollection_ThrowsException()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.ProductExistsAsync(5)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.IsProductInCollectionAsync(1, 5)).ReturnsAsync(true);

        // Act & Assert
        Assert.ThrowsAsync<ProductAlreadyInCollectionException>(
            async () => await _collectionService.AddProductToCollectionAsync(1, 5));
        _collectionRepositoryMock.Verify(r => r.AddProductAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task AddProductToCollectionAsync_ProductNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.ProductExistsAsync(99)).ReturnsAsync(false);

        // Act & Assert
        Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _collectionService.AddProductToCollectionAsync(1, 99));
    }

    // --- RemoveProductFromCollectionAsync ---

    [Test]
    public async Task RemoveProductFromCollectionAsync_ValidIds_RemovesProduct()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.IsProductInCollectionAsync(1, 5)).ReturnsAsync(true);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _collectionService.RemoveProductFromCollectionAsync(1, 5));
        _collectionRepositoryMock.Verify(r => r.RemoveProductAsync(1, 5), Times.Once);
    }

    [Test]
    public async Task RemoveProductFromCollectionAsync_ProductNotInCollection_ThrowsException()
    {
        // Arrange
        _collectionRepositoryMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);
        _collectionRepositoryMock.Setup(r => r.IsProductInCollectionAsync(1, 99)).ReturnsAsync(false);

        // Act & Assert
        Assert.ThrowsAsync<ProductNotInCollectionException>(
            async () => await _collectionService.RemoveProductFromCollectionAsync(1, 99));
        _collectionRepositoryMock.Verify(r => r.RemoveProductAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
