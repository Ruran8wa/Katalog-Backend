using System.Diagnostics.Metrics;
using Katalog_Backend.DTO;
using Katalog_Backend.Enums;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Models;
using Katalog_Backend.Observability;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services;
using Katalog_Backend.Services.Interfaces;
using Katalog_Backend.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static readonly OrderRequester Owner = new("user-1", IsAdmin: false);

    [SetUp]
    public void Setup()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _variantRepositoryMock = new Mock<IVariantRepository>();
        _userManagerMock = MockHelper.MockUserManager<ApplicationUser>();

        _orderService = new OrderService(
            _orderRepositoryMock.Object,
            _variantRepositoryMock.Object,
            _userManagerMock.Object,
            NullLogger<OrderService>.Instance);
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

        var result = await _orderService.GetOrderByIdAsync(10, Owner);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(10));
        Assert.That(result.UserId, Is.EqualTo("user-1"));
        Assert.That(result.TotalPrice, Is.EqualTo(order.Quantity * order.PriceAtPurchase));
    }

    [Test]
    public void GetOrderByIdAsync_NonExistingId_ThrowsOrderNotFoundException()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Order?)null);

        Assert.ThrowsAsync<OrderNotFoundException>(async () => await _orderService.GetOrderByIdAsync(999, Owner));
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
        _orderRepositoryMock.Verify(r => r.CreateWithOutboxAsync(It.IsAny<Order>(), It.IsAny<Func<Order, OutboxMessage>>()), Times.Never);
    }

    [Test]
    public async Task CreateOrderAsync_ValidOrder_CreatesOrderWithPendingStatusAndOutbox()
    {
        var user = CreateDummyUser("user-1");
        var variant = CreateDummyVariant(1, quantity: 10, priceOverride: 150);
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 3, UserId = "user-1" };

        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant);
        _orderRepositoryMock.Setup(r => r.CreateWithOutboxAsync(It.IsAny<Order>(), It.IsAny<Func<Order, OutboxMessage>>()))
            .ReturnsAsync((Order o, Func<Order, OutboxMessage> factory) =>
            {
                o.Id = 1;
                o.User = user;
                o.Variant = variant;
                return o;
            });

        var result = await _orderService.CreateOrderAsync(dto);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(OrderStatus.Pending));
        Assert.That(result.PriceAtPurchase, Is.EqualTo(150));
        Assert.That(result.TotalPrice, Is.EqualTo(450));
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Variant>()), Times.Never);
        _orderRepositoryMock.Verify(r => r.CreateWithOutboxAsync(It.IsAny<Order>(), It.IsAny<Func<Order, OutboxMessage>>()), Times.Once);
    }

    [Test]
    public async Task CreateOrderAsync_PriceOverrideNull_UsesProductBasePrice()
    {
        var user = CreateDummyUser("user-1");
        var variant = CreateDummyVariant(1, quantity: 5, priceOverride: null, basePrice: 85);
        var dto = new CreateOrderDto { VariantId = 1, Quantity = 1 };

        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant);
        _orderRepositoryMock.Setup(r => r.CreateWithOutboxAsync(It.IsAny<Order>(), It.IsAny<Func<Order, OutboxMessage>>()))
            .ReturnsAsync((Order o, Func<Order, OutboxMessage> factory) =>
            {
                o.Id = 1;
                o.User = user;
                o.Variant = variant;
                return o;
            });

        var result = await _orderService.CreateOrderAsync(dto, "user-1");

        Assert.That(result.PriceAtPurchase, Is.EqualTo(85));
    }

    [Test]
    public void UpdateOrderQuantityAsync_InsufficientStockForIncrease_ThrowsInsufficientStockException()
    {
        var order = CreateDummyOrder(1, quantity: 2);
        var variant = CreateDummyVariant(1, quantity: 2); // only 2 available, needs 3 more

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(order.VariantId)).ReturnsAsync(variant);

        Assert.ThrowsAsync<InsufficientStockException>(async () => await _orderService.UpdateOrderQuantityAsync(1, 5, Owner));
        _orderRepositoryMock.Verify(r => r.SaveWithStockChangeAsync(It.IsAny<Order>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void UpdateOrderQuantityAsync_OrderNotFound_ThrowsOrderNotFoundException()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Order?)null);

        Assert.ThrowsAsync<OrderNotFoundException>(async () => await _orderService.UpdateOrderQuantityAsync(999, 3, Owner));
    }

    [Test]
    public void DeleteOrderAsync_NonExistingOrder_ThrowsOrderNotFoundException()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Order?)null);

        Assert.ThrowsAsync<OrderNotFoundException>(async () => await _orderService.DeleteOrderAsync(999, Owner));
        _orderRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [TestCase(OrderStatus.Pending, 0)]
    [TestCase(OrderStatus.Confirmed, 3)]
    public async Task CancelOrderAsync_ReleasesStockOnlyIfItWasReserved(OrderStatus status, int expectedRelease)
    {
        var order = CreateDummyOrder(1, quantity: 3);
        order.Status = status;
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _orderRepositoryMock.Setup(r => r.SaveWithStockChangeAsync(order, It.IsAny<int>())).ReturnsAsync(order);

        var result = await _orderService.CancelOrderAsync(1, Owner);

        Assert.That(result.Status, Is.EqualTo(OrderStatus.Cancelled));
        _orderRepositoryMock.Verify(r => r.SaveWithStockChangeAsync(order, expectedRelease), Times.Once);
    }

    [TestCase(OrderStatus.Failed)]
    [TestCase(OrderStatus.Cancelled)]
    public void CancelOrderAsync_RejectedOrCancelledOrder_ThrowsAndSavesNothing(OrderStatus status)
    {
        var order = CreateDummyOrder(1);
        order.Status = status;
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);

        Assert.ThrowsAsync<OrderStatusConflictException>(() => _orderService.CancelOrderAsync(1, Owner));
        _orderRepositoryMock.Verify(r => r.SaveWithStockChangeAsync(It.IsAny<Order>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task CancelOrderAsync_CountsTheCancellationByWhoAndFromWhichStatus()
    {
        var order = CreateDummyOrder(1, userId: "someone-else");
        order.Status = OrderStatus.Confirmed;
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _orderRepositoryMock.Setup(r => r.SaveWithStockChangeAsync(order, It.IsAny<int>())).ReturnsAsync(order);

        var measurements = new List<Dictionary<string, object?>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument == Telemetry.OrdersCancelled)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            measurements.Add(tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
        listener.Start();

        await _orderService.CancelOrderAsync(1, new OrderRequester("admin-1", IsAdmin: true));

        Assert.That(measurements, Has.Count.EqualTo(1));
        Assert.That(measurements[0], Is.EquivalentTo(new Dictionary<string, object?>
        {
            ["cancelled_by"] = "admin",
            ["from_status"] = "confirmed"
        }));
    }

    [TestCase(2, 5, -3)]
    [TestCase(5, 2, 3)]
    public async Task UpdateOrderQuantityAsync_ConfirmedOrder_ChangesStockByTheDifference(int from, int to, int expectedStockChange)
    {
        var order = CreateDummyOrder(1, quantity: from);
        order.Status = OrderStatus.Confirmed;
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _orderRepositoryMock.Setup(r => r.SaveWithStockChangeAsync(order, It.IsAny<int>())).ReturnsAsync(order);

        var result = await _orderService.UpdateOrderQuantityAsync(1, to, Owner);

        Assert.That(result.Quantity, Is.EqualTo(to));
        _orderRepositoryMock.Verify(r => r.SaveWithStockChangeAsync(order, expectedStockChange), Times.Once);
    }

    [Test]
    public async Task UpdateOrderQuantityAsync_PendingOrder_ChangesNoStock()
    {
        var order = CreateDummyOrder(1, quantity: 2);
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _variantRepositoryMock.Setup(r => r.GetByIdAsync(order.VariantId)).ReturnsAsync(CreateDummyVariant(1, quantity: 10));
        _orderRepositoryMock.Setup(r => r.SaveWithStockChangeAsync(order, It.IsAny<int>())).ReturnsAsync(order);

        await _orderService.UpdateOrderQuantityAsync(1, 5, Owner);

        _orderRepositoryMock.Verify(r => r.SaveWithStockChangeAsync(order, 0), Times.Once);
    }

    [TestCase(OrderStatus.Failed)]
    [TestCase(OrderStatus.Cancelled)]
    public async Task DeleteOrderAsync_RejectedOrCancelledOrder_DeletesWithoutTouchingStock(OrderStatus status)
    {
        var order = CreateDummyOrder(1, quantity: 3);
        order.Status = status;
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
        _orderRepositoryMock.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        await _orderService.DeleteOrderAsync(1, Owner);

        _orderRepositoryMock.Verify(r => r.DeleteAsync(1), Times.Once);
        _variantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Variant>()), Times.Never);
    }

    [TestCase(OrderStatus.Pending)]
    [TestCase(OrderStatus.Confirmed)]
    public void DeleteOrderAsync_PendingOrConfirmedOrder_ThrowsAndDeletesNothing(OrderStatus status)
    {
        var order = CreateDummyOrder(1);
        order.Status = status;
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);

        Assert.ThrowsAsync<OrderStatusConflictException>(() => _orderService.DeleteOrderAsync(1, Owner));
        _orderRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void GetOrderByIdAsync_AnotherCustomersOrder_IsReportedAsMissing()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(CreateDummyOrder(1, userId: "someone-else"));

        Assert.ThrowsAsync<OrderNotFoundException>(() => _orderService.GetOrderByIdAsync(1, Owner));
    }

    [Test]
    public async Task GetOrderByIdAsync_AdminCanReadAnyCustomersOrder()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(CreateDummyOrder(1, userId: "someone-else"));

        var result = await _orderService.GetOrderByIdAsync(1, new OrderRequester("admin-1", IsAdmin: true));

        Assert.That(result.UserId, Is.EqualTo("someone-else"));
    }

    [Test]
    public void CancelOrderAsync_AnotherCustomersOrder_IsReportedAsMissingAndNotSaved()
    {
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(CreateDummyOrder(1, userId: "someone-else"));

        Assert.ThrowsAsync<OrderNotFoundException>(() => _orderService.CancelOrderAsync(1, Owner));
        _orderRepositoryMock.Verify(r => r.SaveWithStockChangeAsync(It.IsAny<Order>(), It.IsAny<int>()), Times.Never);
    }
}
