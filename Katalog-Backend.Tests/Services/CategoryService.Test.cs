using Katalog_Backend.DTO;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services;
using Moq;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Services;

[TestFixture]
public class CategoryServiceTest
{
    private Mock<ICategoryRepo> _categoryRepoMock;
    private CategoryService _categoryService;

    [SetUp]
    public void SetUp()
    {
        _categoryRepoMock = new Mock<ICategoryRepo>();
        _categoryService = new CategoryService(_categoryRepoMock.Object);
    }

    [Test]
    public async Task CreateCategory_WithoutParentId_Success()
    {
        var dto = new CreateCategoryDto
        {
            Name = "Jewelry",
            CategoryParentId = null
        };

        var expectedResponse = new CategoryResponseDto
        {
            Id = 3,
            Name = "Jewelry",
            CategoryParentId = null,
            Children = []
        };
        
        _categoryRepoMock.Setup(c => c.CreateCategory(It.IsAny<CreateCategoryDto>())).ReturnsAsync(expectedResponse);
        
        var createCategory = await _categoryService.CreateCategory(dto);
        
        Assert.That(createCategory, Is.EqualTo(expectedResponse));
        _categoryRepoMock.Verify(c => c.CategoryExists(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task CreateCategory_WithParentId_ParentExists_Success()
    {
        var dto = new CreateCategoryDto
        {
            Name = "Rings",
            CategoryParentId = 1
        };

        var expectedResponse = new CategoryResponseDto
        {
            Id = 4,
            Name = "Rings",
            CategoryParentId = 1,
            Children = []
        };

        _categoryRepoMock.Setup(c => c.CategoryExists(1)).ReturnsAsync(true);
        _categoryRepoMock.Setup(c => c.CreateCategory(dto)).ReturnsAsync(expectedResponse);

        var result = await _categoryService.CreateCategory(dto);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.CategoryParentId, Is.EqualTo(1));
    }

    [Test]
    public void CreateCategory_WithParentId_ParentDoesNotExist_ThrowsKeyNotFoundException()
    {
        var dto = new CreateCategoryDto
        {
            Name = "Rings",
            CategoryParentId = 999
        };

        _categoryRepoMock.Setup(c => c.CategoryExists(999)).ReturnsAsync(false);

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await _categoryService.CreateCategory(dto));
    }

    [Test]
    public void CreateCategory_NoCategoryName_ThrowsArgumentException()
    {
        var dto = new CreateCategoryDto { Name = null! };
        Assert.ThrowsAsync<ArgumentException>(async () => await _categoryService.CreateCategory(dto));
    }

    [Test]
    public async Task GetAllCategories_Success()
    {
        _categoryRepoMock.Setup(c => c.GetAllCategories()).ReturnsAsync(new List<CategoryResponseDto>());
        var getAllCategories = await _categoryService.GetAllCategories();
        Assert.That(getAllCategories, Is.Not.Null);
    }
    
    [Test]
    public async Task GetCategoryById_CorrectId_Success()
    {
        const int categoryId = 2;
        var expectedResult = new CategoryResponseDto
        {
            Id = 2,
            Name = "Jewelry",
            CategoryParentId = 1,
            Children = []
        };
        
        _categoryRepoMock.Setup(c => c.GetCategoryById(categoryId)).ReturnsAsync(expectedResult);
        
        var getCategory = await _categoryService.GetCategoryById(categoryId);
        
        Assert.That(getCategory, Is.Not.Null);
    }

    [Test]
    public void GetCategoryById_WrongId_ThrowsKeyNotFoundException()
    {
        const int wrongCategoryId = 3;
        _categoryRepoMock.Setup(c => c.GetCategoryById(wrongCategoryId))
            .ThrowsAsync(new KeyNotFoundException($"Category with id {wrongCategoryId} was not found."));

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await _categoryService.GetCategoryById(wrongCategoryId));
    }

    [Test]
    public async Task GetCategoryByParentId_CorrectId_Success()
    {
        var parentId = 1;
        var expectedResult = new List<CategoryResponseDto>();
        _categoryRepoMock.Setup(c => c.CategoryExists(parentId)).ReturnsAsync(true);
        _categoryRepoMock.Setup(c => c.GetCategoriesByParentId(parentId)).ReturnsAsync(expectedResult);
        
        var getCategory = await _categoryService.GetCategoriesByParentId(parentId);
        
        Assert.That(getCategory, Is.Not.Null);
    }

    [Test]
    public void GetCategoryByParentId_WrongId_ThrowsKeyNotFoundException()
    {
        const int wrongParentCategoryId = 404;
        _categoryRepoMock.Setup(c => c.CategoryExists(wrongParentCategoryId)).ReturnsAsync(false);

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await _categoryService.GetCategoriesByParentId(wrongParentCategoryId));
    }

    [Test]
    public async Task UpdateCategory_CorrectId_Success()
    {
        var dto = new UpdateCategoryDto
        {
            Id = 3,
            Name = "Jewelry",
        };

        var expectedReturn = new CategoryResponseDto
        {
            Id = 3,
            Name = "Jewelry",
            Children = []
        };
        
        _categoryRepoMock.Setup(c => c.UpdateCategory(dto)).ReturnsAsync(expectedReturn);
        var updateCategory = await _categoryService.UpdateCategory(dto);
        Assert.That(updateCategory, Is.Not.Null);
    }

    [Test]
    public void UpdateCategory_WrongId_ThrowsKeyNotFoundException()
    {
        var dto = new UpdateCategoryDto
        {
            Id = 999,
            Name = "Jewelry",
        };
        
        _categoryRepoMock.Setup(c => c.UpdateCategory(dto))
            .ThrowsAsync(new KeyNotFoundException($"Category with id {dto.Id} was not found."));
        
        Assert.ThrowsAsync<KeyNotFoundException>(async () => await _categoryService.UpdateCategory(dto));
    }

    [Test]
    public async Task DeleteCategory_CorrectId_Success()
    {
        const int categoryId = 1;
        _categoryRepoMock.Setup(c => c.DeleteCategory(categoryId)).Returns(Task.CompletedTask);

        await _categoryService.DeleteCategory(categoryId);

        _categoryRepoMock.Verify(c => c.DeleteCategory(categoryId), Times.Once);
    }

    [Test]
    public void DeleteCategory_WrongId_ThrowsKeyNotFoundException()
    {
        const int wrongCategoryId = 999;
        _categoryRepoMock.Setup(c => c.DeleteCategory(wrongCategoryId))
            .ThrowsAsync(new KeyNotFoundException($"Category with id {wrongCategoryId} was not found."));

        Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _categoryService.DeleteCategory(wrongCategoryId));
    }
}
