using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Services;

[TestFixture]
public class VariantServiceTests
{
    private Mock<IVariantRepository> _variantRepositoryMock;
    private Mock<IFileStorageService> _fileStorageServiceMock;
    private VariantService _variantService;

    [SetUp]
    public void Setup()
    {
        _variantRepositoryMock = new Mock<IVariantRepository>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();

        _variantService = new VariantService(
            _variantRepositoryMock.Object,
            _fileStorageServiceMock.Object);
    }

    private static Product CreateDummyProduct(int id = 1, string name = "Test Product")
    {
        var category = new Category { Id = 1, CategoryName = "Test Category" };
        return new Product
        {
            Id = id,
            Name = name,
            Description = "Desc",
            BasePrice = 100,
            Material = "Cotton",
            CategoryId = 1,
            Category = category,
            Variants = new List<Variant>()
        };
    }

    [Test]
    public async Task GetAllVariantsAsync_ReturnsMappedDtoList()
    {
        // Arrange
        var product = CreateDummyProduct(1);
        var variants = new List<Variant>
        {
            new() { Id = 1, Name = "Red / L", Sku = "SKU-1", Quantity = 10, ProductId = 1, Product = product },
            new() { Id = 2, Name = "Blue / M", Sku = "SKU-2", Quantity = 5, ProductId = 1, Product = product }
        };

        _variantRepositoryMock.Setup(r => r.GetAllAsync(null)).ReturnsAsync(variants);

        // Act
        var result = await _variantService.GetAllVariantsAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0].Sku, Is.EqualTo("SKU-1"));
        Assert.That(list[1].Sku, Is.EqualTo("SKU-2"));
    }

    [Test]
    public async Task GetAllVariantsAsync_WithInvalidProductId_ThrowsKeyNotFoundException()
    {
        // Arrange
        _variantRepositoryMock.Setup(r => r.ProductExistsAsync(99)).ReturnsAsync(false);

        // Act & Assert
        Assert.ThrowsAsync<KeyNotFoundException>(async () => await _variantService.GetAllVariantsAsync(99));
    }

    [Test]
    public async Task GetVariantByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        var product = CreateDummyProduct(1);
        var variant = new Variant
        {
            Id = 10,
            Name = "Black / S",
            Sku = "BLK-S",
            Quantity = 20,
            ProductId = 1,
            Product = product
        };

        _variantRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(variant);

        // Act
        var result = await _variantService.GetVariantByIdAsync(10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(10));
        Assert.That(result.Sku, Is.EqualTo("BLK-S"));
    }

    [Test]
    public async Task GetVariantByIdAsync_NonExistingId_ThrowsVariantNotFoundException()
    {
        // Arrange
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Variant?)null);

        // Act & Assert
        Assert.ThrowsAsync<VariantNotFoundException>(async () => await _variantService.GetVariantByIdAsync(99));
    }

    [Test]
    public async Task CreateVariantAsync_ValidDto_CreatesAndReturnsDto()
    {
        // Arrange
        var product = CreateDummyProduct(1, "Shirt");
        var createDto = new CreateVariantDto
        {
            Name = "Green / XL",
            Sku = "GRN-XL",
            Quantity = 15,
            ProductId = 1
        };

        var createdVariant = new Variant
        {
            Id = 5,
            Name = createDto.Name,
            Sku = createDto.Sku,
            Quantity = createDto.Quantity,
            ProductId = createDto.ProductId,
            Product = product
        };

        _variantRepositoryMock.Setup(r => r.ProductExistsAsync(1)).ReturnsAsync(true);
        _variantRepositoryMock.Setup(r => r.SkuExistsAsync("GRN-XL", null)).ReturnsAsync(false);
        _variantRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Variant>())).ReturnsAsync(createdVariant);

        // Act
        var result = await _variantService.CreateVariantAsync(createDto);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(5));
        Assert.That(result.Sku, Is.EqualTo("GRN-XL"));
        _variantRepositoryMock.Verify(r => r.CreateAsync(It.Is<Variant>(v => v.Sku == "GRN-XL")), Times.Once);
    }

    [Test]
    public async Task CreateVariantAsync_DuplicateSku_ThrowsDuplicateSkuException()
    {
        // Arrange
        var createDto = new CreateVariantDto { Name = "V1", Sku = "DUP-SKU", ProductId = 1 };

        _variantRepositoryMock.Setup(r => r.ProductExistsAsync(1)).ReturnsAsync(true);
        _variantRepositoryMock.Setup(r => r.SkuExistsAsync("DUP-SKU", null)).ReturnsAsync(true);

        // Act & Assert
        Assert.ThrowsAsync<DuplicateSkuException>(async () => await _variantService.CreateVariantAsync(createDto));
    }

    [Test]
    public async Task UpdateVariantAsync_ValidDto_UpdatesAndReturnsDto()
    {
        // Arrange
        var product = CreateDummyProduct(1);
        var existing = new Variant
        {
            Id = 1,
            Name = "Old",
            Sku = "OLD-SKU",
            Quantity = 5,
            ProductId = 1,
            Product = product
        };

        var updateDto = new UpdateVariantDto
        {
            Name = "New Name",
            Sku = "NEW-SKU",
            Quantity = 12
        };

        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);
        _variantRepositoryMock.Setup(r => r.SkuExistsAsync("NEW-SKU", 1)).ReturnsAsync(false);
        _variantRepositoryMock.Setup(r => r.UpdateAsync(existing)).ReturnsAsync(existing);

        // Act
        var result = await _variantService.UpdateVariantAsync(1, updateDto);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("New Name"));
        Assert.That(result.Sku, Is.EqualTo("NEW-SKU"));
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Variant>(v => v.Sku == "NEW-SKU")), Times.Once);
    }

    [Test]
    public async Task DeleteVariantAsync_ExistingVariant_DeletesVariantAndImages()
    {
        // Arrange
        var product = CreateDummyProduct(1);
        var variant = new Variant
        {
            Id = 1,
            Name = "V1",
            Sku = "SKU1",
            ProductId = 1,
            Product = product,
            Images = new List<VariantImage>
            {
                new() { Id = 10, ImageUrl = "http://img1.jpg", PublicId = "pub1", VariantId = 1, Variant = null! }
            }
        };

        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant);
        _variantRepositoryMock.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);
        _fileStorageServiceMock.Setup(s => s.DeleteImageAsync("pub1")).ReturnsAsync(true);

        // Act
        await _variantService.DeleteVariantAsync(1);

        // Assert
        _fileStorageServiceMock.Verify(s => s.DeleteImageAsync("pub1"), Times.Once);
        _variantRepositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Test]
    public async Task UploadVariantImageAsync_ValidFile_UploadsAndReturnsDto()
    {
        // Arrange
        var product = CreateDummyProduct(1);
        var variant = new Variant
        {
            Id = 1,
            Name = "V1",
            Sku = "SKU1",
            ProductId = 1,
            Product = product,
            Images = new List<VariantImage>()
        };

        var fileMock = new Mock<IFormFile>();
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant);
        _fileStorageServiceMock.Setup(s => s.UploadImageAsync(fileMock.Object, "variants"))
            .ReturnsAsync(new StorageUploadResult("https://cloudinary.com/img.jpg", "pub_123"));

        _variantRepositoryMock.Setup(r => r.AddImageAsync(It.IsAny<VariantImage>()))
            .ReturnsAsync((VariantImage img) => { img.Id = 100; return img; });

        // Act
        var result = await _variantService.UploadVariantImageAsync(1, fileMock.Object, isPrimary: true);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ImageUrl, Is.EqualTo("https://cloudinary.com/img.jpg"));
        Assert.That(result.PublicId, Is.EqualTo("pub_123"));
        Assert.That(result.IsPrimary, Is.True);
        _fileStorageServiceMock.Verify(s => s.UploadImageAsync(fileMock.Object, "variants"), Times.Once);
    }
}
