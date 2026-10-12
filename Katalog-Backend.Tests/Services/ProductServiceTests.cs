using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services;
using Moq;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Services;

[TestFixture]
public class ProductServiceTests
{
    private Mock<IProductRepository> _productRepositoryMock;
    private ProductService _productService;

    [SetUp]
    public void Setup()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _productService = new ProductService(_productRepositoryMock.Object);
    }

    private static (List<Product> products, Category category) CreateDummyProducts()
    {
        var category = new Category { Id = 1, CategoryName = "Electronics" };
        var products = new List<Product>
        {
            new() { Id = 1, Name = "Laptop", Description = "Gaming Laptop", BasePrice = 1200, Material = "Aluminum", CategoryId = 1, Category = category, Variants = new List<Variant>() },
            new() { Id = 2, Name = "Mouse", Description = "Wireless Mouse", BasePrice = 50, Material = "Plastic", CategoryId = 1, Category = category, Variants = new List<Variant>() }
        };
        return (products, category);
    }

    // ── GetAllProductsAsync ───────────────────────────────────────────────────

    [Test]
    public async Task GetAllProductsAsync_ReturnsMappedDtoList()
    {
        var (products, _) = CreateDummyProducts();
        _productRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(products);

        var result = await _productService.GetAllProductsAsync();

        Assert.That(result, Is.Not.Null);
        var resultList = result.ToList();
        Assert.That(resultList, Has.Count.EqualTo(2));
        Assert.That(resultList[0].Name, Is.EqualTo("Laptop"));
        Assert.That(resultList[0].CategoryName, Is.EqualTo("Electronics"));
        _productRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ── GetProductsAsync (paged) ──────────────────────────────────────────────

    [Test]
    public async Task GetProductsAsync_NoFilters_ReturnsPagedResult()
    {
        var (products, _) = CreateDummyProducts();
        var query = new ProductQueryDto { Page = 1, PageSize = 20 };

        _productRepositoryMock
            .Setup(r => r.GetPagedAsync(query))
            .ReturnsAsync((products, 2));

        var result = await _productService.GetProductsAsync(query);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Page, Is.EqualTo(1));
        Assert.That(result.PageSize, Is.EqualTo(20));
        Assert.That(result.Items.Count(), Is.EqualTo(2));
        Assert.That(result.TotalPages, Is.EqualTo(1));
        Assert.That(result.HasNextPage, Is.False);
        Assert.That(result.HasPreviousPage, Is.False);
    }

    [Test]
    public async Task GetProductsAsync_WithPaging_CalculatesMetaCorrectly()
    {
        var (products, _) = CreateDummyProducts();
        var query = new ProductQueryDto { Page = 2, PageSize = 1 };

        _productRepositoryMock
            .Setup(r => r.GetPagedAsync(query))
            .ReturnsAsync((products.Take(1).ToList(), 2));

        var result = await _productService.GetProductsAsync(query);

        Assert.That(result.TotalPages, Is.EqualTo(2));
        Assert.That(result.HasPreviousPage, Is.True);
        Assert.That(result.HasNextPage, Is.False);
    }

    [Test]
    public async Task GetProductsAsync_EmptyResult_ReturnsEmptyPagedResult()
    {
        var query = new ProductQueryDto { Page = 1, PageSize = 20, Search = "nonexistent" };
        _productRepositoryMock
            .Setup(r => r.GetPagedAsync(query))
            .ReturnsAsync((new List<Product>(), 0));

        var result = await _productService.GetProductsAsync(query);

        Assert.That(result.TotalCount, Is.EqualTo(0));
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalPages, Is.EqualTo(0));
        Assert.That(result.HasNextPage, Is.False);
        Assert.That(result.HasPreviousPage, Is.False);
    }

    [Test]
    public async Task GetProductsAsync_PassesQueryToRepository()
    {
        var query = new ProductQueryDto
        {
            Search = "Laptop",
            CategoryId = 1,
            Material = "Aluminum",
            MinPrice = 100,
            MaxPrice = 2000,
            SortBy = "price",
            SortDirection = "desc",
            Page = 1,
            PageSize = 10
        };

        _productRepositoryMock
            .Setup(r => r.GetPagedAsync(query))
            .ReturnsAsync((new List<Product>(), 0));

        await _productService.GetProductsAsync(query);

        _productRepositoryMock.Verify(r => r.GetPagedAsync(query), Times.Once);
    }

    // ── GetProductByIdAsync ───────────────────────────────────────────────────

    [Test]
    public async Task GetProductByIdAsync_ExistingId_ReturnsDto()
    {
        var category = new Category { Id = 1, CategoryName = "Electronics" };
        var product = new Product { Id = 10, Name = "Phone", Description = "Smartphone", BasePrice = 800, Material = "Glass", CategoryId = 1, Category = category, Variants = new List<Variant>() };

        _productRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(product);

        var result = await _productService.GetProductByIdAsync(10);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(10));
        Assert.That(result.Name, Is.EqualTo("Phone"));
        Assert.That(result.CategoryName, Is.EqualTo("Electronics"));
        _productRepositoryMock.Verify(r => r.GetByIdAsync(10), Times.Once);
    }

    [Test]
    public async Task GetProductByIdAsync_NonExistingId_ThrowsProductNotFoundException()
    {
        _productRepositoryMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Product?)null);

        var ex = Assert.ThrowsAsync<ProductNotFoundException>(async () => await _productService.GetProductByIdAsync(99));
        Assert.That(ex!.Message, Contains.Substring("99"));
        _productRepositoryMock.Verify(r => r.GetByIdAsync(99), Times.Once);
    }

    // ── CreateProductAsync ────────────────────────────────────────────────────

    [Test]
    public async Task CreateProductAsync_ValidDto_CreatesAndReturnsDto()
    {
        var createDto = new CreateProductDto { Name = "Tablet", Description = "10 inch Tablet", BasePrice = 300, Material = "Plastic", CategoryId = 2 };
        var createdProduct = new Product { Id = 5, Name = createDto.Name, Description = createDto.Description, BasePrice = createDto.BasePrice, Material = createDto.Material, CategoryId = createDto.CategoryId, Category = new Category { Id = 2, CategoryName = "Gadgets" }, Variants = new List<Variant>() };

        _productRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Product>())).ReturnsAsync(createdProduct);

        var result = await _productService.CreateProductAsync(createDto);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(5));
        Assert.That(result.Name, Is.EqualTo("Tablet"));
        Assert.That(result.CategoryName, Is.EqualTo("Gadgets"));
        _productRepositoryMock.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Name == createDto.Name && p.CategoryId == createDto.CategoryId)), Times.Once);
    }

    // ── UpdateProductAsync ────────────────────────────────────────────────────

    [Test]
    public async Task UpdateProductAsync_ExistingId_UpdatesAndReturnsDto()
    {
        var updateDto = new UpdateProductDto { Name = "Pro Laptop", Description = "Upgraded Laptop", BasePrice = 1500, Material = "Aluminum", CategoryId = 1 };
        var existingProduct = new Product { Id = 1, Name = "Old Laptop", Description = "Old", BasePrice = 1000, Material = "Aluminum", CategoryId = 1, Category = new Category { Id = 1, CategoryName = "Electronics" }, Variants = new List<Variant>() };

        _productRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingProduct);
        _productRepositoryMock.Setup(r => r.UpdateAsync(existingProduct)).ReturnsAsync(existingProduct);

        var result = await _productService.UpdateProductAsync(1, updateDto);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("Pro Laptop"));
        Assert.That(result.BasePrice, Is.EqualTo(1500));
        _productRepositoryMock.Verify(r => r.GetByIdAsync(1), Times.Once);
        _productRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Product>(p => p.Name == "Pro Laptop")), Times.Once);
    }

    [Test]
    public async Task UpdateProductAsync_NonExistingId_ThrowsProductNotFoundException()
    {
        var updateDto = new UpdateProductDto { Name = "Item", CategoryId = 1 };
        _productRepositoryMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Product?)null);

        Assert.ThrowsAsync<ProductNotFoundException>(async () => await _productService.UpdateProductAsync(99, updateDto));
        _productRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    // ── DeleteProductAsync ────────────────────────────────────────────────────

    [Test]
    public async Task DeleteProductAsync_ExistingId_DeletesSuccessfully()
    {
        _productRepositoryMock.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        Assert.DoesNotThrowAsync(async () => await _productService.DeleteProductAsync(1));
        _productRepositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Test]
    public async Task DeleteProductAsync_NonExistingId_ThrowsProductNotFoundException()
    {
        _productRepositoryMock.Setup(r => r.DeleteAsync(99)).ReturnsAsync(false);

        Assert.ThrowsAsync<ProductNotFoundException>(async () => await _productService.DeleteProductAsync(99));
        _productRepositoryMock.Verify(r => r.DeleteAsync(99), Times.Once);
    }
}

