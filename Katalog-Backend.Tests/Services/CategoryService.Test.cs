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
    public async Task CreateCategory_CorrectData_Success()
    {
        var dto = new CreateCategoryDto
        {
            Name = "Jewelry",
            CategoryParentId = 1
        };

        var expectedResponse = new CategoryResponseDto
        {
            Id = 3,
            Name = "Jewelry",
            CategoryParentId = 1,
            Children = []
        };
        
        _categoryRepoMock.Setup(c => c.CreateCategory(It.IsAny<CreateCategoryDto>())).ReturnsAsync(expectedResponse);
        
        var createCategory = await _categoryService.CreateCategory(dto);
        Assert.That(createCategory, Is.EqualTo(expectedResponse));
    }

    [Test]
    public void CreateCategory_NoCategoryName_ThrowsException()
    {
        var dto = new CreateCategoryDto { Name = null };
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _categoryService.CreateCategory(dto));
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
    public void GetCategoryById_WrongId_ThrowsException()
    {
        const int wrongCategoryId = 3;
        _categoryRepoMock.Setup(c => c.GetCategoryById(wrongCategoryId))!
            .ReturnsAsync((CategoryResponseDto)null!);
        Assert.ThrowsAsync<InvalidDataException>(async () => await _categoryService.GetCategoryById(wrongCategoryId));
    }

    [Test]
    public async Task GetCategoryByParentId_CorrectId_Success()
    {
        var parentId = 001;
        var expectedResult = new List<CategoryResponseDto>();
        _categoryRepoMock.Setup(c => c.GetCategoriesByParentId(parentId)).ReturnsAsync(expectedResult);
        var getCategory = await _categoryService.GetCategoriesByParentId(parentId);
        Assert.That(getCategory, Is.Not.Null);
    }

    [Test]
    public void GetCategoryByParentId_WrongId_ThrowsException()
    {
        const int wrongParentCategoryId = 404;
        _categoryRepoMock.Setup(c => c.GetCategoriesByParentId(wrongParentCategoryId))
            .ReturnsAsync((List<CategoryResponseDto>)null!);
        Assert.ThrowsAsync<InvalidDataException>(async () => await _categoryService.GetCategoriesByParentId(wrongParentCategoryId));
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
            Name = "Clothing",
            Children = []
        };
        
        _categoryRepoMock.Setup(c => c.UpdateCategory(dto)).ReturnsAsync(expectedReturn);
        var updateCategory = await _categoryService.UpdateCategory(dto);
        Assert.That(updateCategory, Is.Not.Null);
    }

    [Test]
    public void UpdateCategory_WrongId_ThrowsException()
    {
        var dto = new UpdateCategoryDto
        {
            Id = 001,
            Name = "Jewelry",
        };
        
        _categoryRepoMock.Setup(c => c.UpdateCategory(dto))
            .ReturnsAsync((CategoryResponseDto)null!);
        
        Assert.ThrowsAsync<InvalidDataException>(async () => await _categoryService.UpdateCategory(dto));
    }

    [Test]
    public async Task DeleteCategory_CorrectId_Success()
    {
        const int categoryId = 001;
        _categoryRepoMock.Setup(c => c.DeleteCategory(categoryId));
        await _categoryService.DeleteCategory(categoryId);
        _categoryRepoMock.Verify(c => c.DeleteCategory(categoryId), Times.Once);
    }
    
    [Test]
    public void DeleteCategory_WrongId_ThrowsException()
    {
        const int wrongCategoryId = 1;
        _categoryRepoMock.Setup(c => c.GetCategoryById(wrongCategoryId))
            .ReturnsAsync((CategoryResponseDto)null!);
        Assert.ThrowsAsync<InvalidDataException>(async ()=> await _categoryService.DeleteCategory(wrongCategoryId));
    }
}
