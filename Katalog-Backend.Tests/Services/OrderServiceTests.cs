using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services;
using Katalog_Backend.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Moq;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Services;

[TestFixture]
public class OrderServiceTests
{
    private Mock<IOrderRepository> _orderRepositoryMock;
    private Mock<IVariantRepository> _variantRepositoryMock;
    private Mock<UserManager<ApplicationUser>> _userManagerMock;
    private OrderService _orderService;

    [SetUp]
    public void Setup()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _variantRepositoryMock = new Mock<IVariantRepository>();
        _userManagerMock = MockHelper.MockUserManager<ApplicationUser>();

        _orderService = new OrderService(
            _orderRepositoryMock.Object,
            _variantRepositoryMock.Object,
            _userManagerMock.Object);
    }

    private static ApplicationUser CreateDummyUser(string id = "user-1", string email = "user@test.com")
    {
        return new ApplicationUser
        {
            Id = id,
            Email = email,
            UserName = email,
            FirstName = "Test",
            LastName = "User"
        };
    }

    private static Variant CreateDummyVariant(int id = 1, int quantity = 10, int? priceOverride = null, int basePrice = 100)
    {
        var product = new Product
        {
            Id = 1,
            Name = "Test Product",
            BasePrice = basePrice,
            Category = new Category { Id = 1, CategoryName = "Cat" },
            Variants = new List<Variant>()
        };

        return new Variant
        {
            Id = id,
            Name = "Variant A",
            Sku = "SKU-001",
            Quantity = quantity,
            PriceOverride = priceOverride,
            ProductId = product.Id,
            Product = product
        };
    }

    private static Order CreateDummyOrder(int id = 1, string userId = "user-1", int variantId = 1, int quantity = 2, int price = 100)
    {
        var user = CreateDummyUser(userId);
        var variant = CreateDummyVariant(variantId);

        return new Order
        {
            Id = id,
            UserId = userId,
            VariantId = variantId,
            Quantity = quantity,
            PriceAtPurchase = price,
            CreatedAt = DateTime.UtcNow,
            User = user,
            Variant = variant
        };
    }

    [Test]
    public async Task GetAllOrdersAsync_ReturnsMappedOrders()
    {
        var orders = new List<Order> { CreateDummyOrder(1), CreateDummyOrder(2) };
        _orderRepositoryMock.Setup(r => r.GetAllAsync(null)).ReturnsAsync(orders);

        var result = await _orderService.GetAllOrdersAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllOrdersAsync_WithUserId_PassesUserIdToRepository()
    {
        var orders = new List<Order> { CreateDummyOrder(1, "user-42") };
        _orderRepositoryMock.Setup(r => r.GetAllAsync("user-42")).ReturnsAsync(orders);

        var result = await _orderService.GetAllOrdersAsync("user-42");

        Assert.That(result.Count(), Is.EqualTo(1));
        _orderRepositoryMock.Verify(r => r.GetAllAsync("user-42"), Times.Once);
    }

    [Test]
    public async Task GetOrderByIdAsync_ExistingId_ReturnsDto()
    {
        var order = CreateDummyOrder(10);
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(order);

        var result = await _orderService.GetOrderByIdAsync(10);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(10));
        Assert.That(result.UserId, Is.EqualTo("user-1"));
        Assert.That(result.TotalPrice, Is.EqualTo(order.Quantity * order.PriceAtPurchase));
    }

    [Test]
    public void GetOrderByIdAsync_NonExistingId_ThrowsOrderNotFoundException()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Order?)null);

        Assert.ThrowsAsync<OrderNotFoundException>(async () => await _orderService.GetOrderByIdAsync(999));
    }

    [Test]
    public void CreateOrderAsync_MissingUserId_ThrowsArgumentException()
    {
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 2, UserId = null };

        Assert.ThrowsAsync<ArgumentException>(async () => await _orderService.CreateOrderAsync(dto, null));
    }

    [Test]
    public void CreateOrderAsync_ZeroOrNegativeQuantity_ThrowsArgumentException()
    {
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 0, UserId = "user-1" };

        Assert.ThrowsAsync<ArgumentException>(async () => await _orderService.CreateOrderAsync(dto));
    }

    [Test]
    public void CreateOrderAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 1, UserId = "nonexistent-user" };
        _userManagerMock.Setup(m => m.FindByIdAsync("nonexistent-user")).ReturnsAsync((ApplicationUser?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await _orderService.CreateOrderAsync(dto));
    }

    [Test]
    public void CreateOrderAsync_VariantNotFound_ThrowsVariantNotFoundException()
    {
        var user = CreateDummyUser("user-1");
        var dto = new CreateOrderDto { VariantId = 999, Quantity = 1, UserId = "user-1" };

        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Variant?)null);

        Assert.ThrowsAsync<VariantNotFoundException>(async () => await _orderService.CreateOrderAsync(dto));
    }

    [Test]
    public void CreateOrderAsync_InsufficientStock_ThrowsInsufficientStockException()
    {
        var user = CreateDummyUser("user-1");
        var variant = CreateDummyVariant(1, quantity: 2);
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 5, UserId = "user-1" };

        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant);

        Assert.ThrowsAsync<InsufficientStockException>(async () => await _orderService.CreateOrderAsync(dto));
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Variant>()), Times.Never);
        _orderRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public async Task CreateOrderAsync_ValidOrder_DeductsStockAndCreatesOrder()
    {
        var user = CreateDummyUser("user-1");
        var variant = CreateDummyVariant(1, quantity: 10, priceOverride: 150);
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 3, UserId = "user-1" };

        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant);
        _orderRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Order>()))
            .ReturnsAsync((Order o) => { o.Id = 1; o.User = user; o.Variant = variant; return o; });

        var result = await _orderService.CreateOrderAsync(dto);

        Assert.That(result, Is.Not.Null);
        Assert.That(variant.Quantity, Is.EqualTo(7));
        Assert.That(result.PriceAtPurchase, Is.EqualTo(150));
        Assert.That(result.TotalPrice, Is.EqualTo(450));
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Variant>(v => v.Quantity == 7)), Times.Once);
        _orderRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<Order>()), Times.Once);
    }

    [Test]
    public async Task CreateOrderAsync_PriceOverrideNull_UsesProductBasePrice()
    {
        var user = CreateDummyUser("user-1");
        var variant = CreateDummyVariant(1, quantity: 5, priceOverride: null, basePrice: 85);
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 1 };

        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant);
        _orderRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Order>()))
            .ReturnsAsync((Order o) => { o.Id = 1; o.User = user; o.Variant = variant; return o; });

        var result = await _orderService.CreateOrderAsync(dto, "user-1");

        Assert.That(result.PriceAtPurchase, Is.EqualTo(85));
    }

    [Test]
    public async Task UpdateOrderQuantityAsync_IncreaseQuantity_DeductsDifferenceFromStock()
    {
        var order = CreateDummyOrder(1, quantity: 2);
        var variant = CreateDummyVariant(1, quantity: 10);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(order.VariantId)).ReturnsAsync(variant);
        _orderRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Order>())).ReturnsAsync(order);

        var result = await _orderService.UpdateOrderQuantityAsync(1, 5);

        Assert.That(order.Quantity, Is.EqualTo(5));
        Assert.That(variant.Quantity, Is.EqualTo(7)); // 10 - (5 - 2) = 7
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Variant>(v => v.Quantity == 7)), Times.Once);
        _orderRepositoryMock.Verify(r => r.UpdateAsync(order), Times.Once);
    }

    [Test]
    public async Task UpdateOrderQuantityAsync_DecreaseQuantity_RestoresDifferenceToStock()
    {
        var order = CreateDummyOrder(1, quantity: 5);
        var variant = CreateDummyVariant(1, quantity: 10);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(order.VariantId)).ReturnsAsync(variant);
        _orderRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Order>())).ReturnsAsync(order);

        var result = await _orderService.UpdateOrderQuantityAsync(1, 2);

        Assert.That(order.Quantity, Is.EqualTo(2));
        Assert.That(variant.Quantity, Is.EqualTo(13)); // 10 + (5 - 2) = 13
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Variant>(v => v.Quantity == 13)), Times.Once);
        _orderRepositoryMock.Verify(r => r.UpdateAsync(order), Times.Once);
    }

    [Test]
    public void UpdateOrderQuantityAsync_InsufficientStockForIncrease_ThrowsInsufficientStockException()
    {
        var order = CreateDummyOrder(1, quantity: 2);
        var variant = CreateDummyVariant(1, quantity: 2); // only 2 available, needs 3 more

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(order.VariantId)).ReturnsAsync(variant);

        Assert.ThrowsAsync<InsufficientStockException>(async () => await _orderService.UpdateOrderQuantityAsync(1, 5));
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Variant>()), Times.Never);
    }

    [Test]
    public void UpdateOrderQuantityAsync_OrderNotFound_ThrowsOrderNotFoundException()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Order?)null);

        Assert.ThrowsAsync<OrderNotFoundException>(async () => await _orderService.UpdateOrderQuantityAsync(999, 3));
    }

    [Test]
    public async Task DeleteOrderAsync_ExistingOrder_RestoresStockAndDeletesOrder()
    {
        var order = CreateDummyOrder(1, quantity: 3);
        var variant = CreateDummyVariant(1, quantity: 7);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(order.VariantId)).ReturnsAsync(variant);
        _orderRepositoryMock.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        await _orderService.DeleteOrderAsync(1);

        Assert.That(variant.Quantity, Is.EqualTo(10)); // 7 + 3
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Variant>(v => v.Quantity == 10)), Times.Once);
        _orderRepositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Test]
    public void DeleteOrderAsync_NonExistingOrder_ThrowsOrderNotFoundException()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Order?)null);

        Assert.ThrowsAsync<OrderNotFoundException>(async () => await _orderService.DeleteOrderAsync(999));
        _orderRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }
}
