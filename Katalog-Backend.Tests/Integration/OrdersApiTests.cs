using System.Net;
using System.Net.Http.Json;
using Katalog_Backend.DTO;
using Katalog_Backend.Enums;
using Katalog_Backend.Tests.Helpers;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class OrdersApiTests
{
    private KatalogApiFactory _factory = null!;
    private TestCaller _owner = null!;
    private TestCaller _otherCustomer = null!;
    private TestCaller _admin = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _factory = KatalogApiFactory.Create();
        _owner = await _factory.CreateCustomerAsync();
        _otherCustomer = await _factory.CreateCustomerAsync();
        _admin = await _factory.CreateAdminAsync();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _owner.Client.Dispose();
        _otherCustomer.Client.Dispose();
        _admin.Client.Dispose();
        _factory.Dispose();
    }

    private async Task<int> SeedOwnersOrderAsync(OrderStatus status = OrderStatus.Pending, int quantity = 2, int stock = 10)
    {
        var variant = await TestDatabase.SeedVariantAsync(stock);
        var order = await TestDatabase.SeedOrderAsync(_owner.UserId, variant.Id, quantity, status);
        return order.Id;
    }

    [Test]
    public async Task GetById_OwnOrder_Returns200()
    {
        var orderId = await SeedOwnersOrderAsync();

        using var response = await _owner.Client.GetAsync($"/api/orders/{orderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var order = await response.Content.ReadFromJsonAsync<OrderResponseDto>();
        Assert.That(order!.Id, Is.EqualTo(orderId));
    }

    [Test]
    public async Task GetById_AnotherCustomersOrder_Returns404LikeAMissingOrder()
    {
        var orderId = await SeedOwnersOrderAsync();

        using var response = await _otherCustomer.Client.GetAsync($"/api/orders/{orderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_AdminCanReadAnyOrder()
    {
        var orderId = await SeedOwnersOrderAsync();

        using var response = await _admin.Client.GetAsync($"/api/orders/{orderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetById_NotLoggedIn_Returns401()
    {
        var orderId = await SeedOwnersOrderAsync();
        using var anonymous = _factory.CreateAnonymousClient();

        using var response = await anonymous.GetAsync($"/api/orders/{orderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UpdateQuantity_AnotherCustomersOrder_Returns404()
    {
        var orderId = await SeedOwnersOrderAsync();

        using var response = await _otherCustomer.Client.PutAsJsonAsync(
            $"/api/orders/{orderId}/quantity", new UpdateOrderQuantityDto { Quantity = 3 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private async Task<(int OrderId, int VariantId)> SeedOrderWithVariantAsync(
        string userId, OrderStatus status, int quantity, int stock)
    {
        var variant = await TestDatabase.SeedVariantAsync(stock);
        var order = await TestDatabase.SeedOrderAsync(userId, variant.Id, quantity, status);
        return (order.Id, variant.Id);
    }

    [Test]
    public async Task Cancel_OwnPendingOrder_CancelsItWithoutTouchingStock()
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Pending, quantity: 2, stock: 10);

        using var response = await _owner.Client.PostAsync($"/api/orders/{orderId}/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var order = await response.Content.ReadFromJsonAsync<OrderResponseDto>();
        Assert.That(order!.Status, Is.EqualTo(OrderStatus.Cancelled));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(10));
    }

    [Test]
    public async Task Cancel_OwnConfirmedOrder_ReleasesItsStock()
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Confirmed, quantity: 3, stock: 7);

        using var response = await _owner.Client.PostAsync($"/api/orders/{orderId}/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await TestDatabase.GetOrderAsync(orderId)).Status, Is.EqualTo(OrderStatus.Cancelled));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(10));
    }

    [Test]
    public async Task Cancel_AnotherCustomersOrder_Returns404AndLeavesItAlone()
    {
        var (orderId, _) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Pending, quantity: 2, stock: 10);

        using var response = await _otherCustomer.Client.PostAsync($"/api/orders/{orderId}/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await TestDatabase.GetOrderAsync(orderId)).Status, Is.EqualTo(OrderStatus.Pending));
    }

    [Test]
    public async Task Cancel_AdminCanCancelAnyCustomersOrder()
    {
        var (orderId, _) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Pending, quantity: 2, stock: 10);

        using var response = await _admin.Client.PostAsync($"/api/orders/{orderId}/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase(OrderStatus.Failed)]
    [TestCase(OrderStatus.Cancelled)]
    public async Task Cancel_RejectedOrAlreadyCancelledOrder_Returns409AndLeavesStockAlone(OrderStatus status)
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, status, quantity: 2, stock: 10);

        using var response = await _owner.Client.PostAsync($"/api/orders/{orderId}/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await TestDatabase.GetOrderAsync(orderId)).Status, Is.EqualTo(status));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(10));
    }

    [Test]
    public async Task Cancel_MissingOrder_Returns404()
    {
        using var response = await _owner.Client.PostAsync("/api/orders/999999999/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Cancel_NotLoggedIn_Returns401()
    {
        var (orderId, _) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Pending, quantity: 2, stock: 10);
        using var anonymous = _factory.CreateAnonymousClient();

        using var response = await anonymous.PostAsync($"/api/orders/{orderId}/cancel", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Delete_ByACustomer_IsForbiddenEvenForTheirOwnOrder()
    {
        var (orderId, _) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Cancelled, quantity: 2, stock: 10);

        using var response = await _owner.Client.DeleteAsync($"/api/orders/{orderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase(OrderStatus.Pending)]
    [TestCase(OrderStatus.Confirmed)]
    public async Task Delete_PendingOrConfirmedOrder_Returns409AndKeepsIt(OrderStatus status)
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, status, quantity: 2, stock: 10);

        using var response = await _admin.Client.DeleteAsync($"/api/orders/{orderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await TestDatabase.GetOrderAsync(orderId)).Status, Is.EqualTo(status));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(10));
    }

    [TestCase(OrderStatus.Failed)]
    [TestCase(OrderStatus.Cancelled)]
    public async Task Delete_RejectedOrCancelledOrder_ByAdmin_RemovesItWithoutTouchingStock(OrderStatus status)
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, status, quantity: 2, stock: 10);

        using var response = await _admin.Client.DeleteAsync($"/api/orders/{orderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var lookup = await _admin.Client.GetAsync($"/api/orders/{orderId}");
        Assert.That(lookup.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(10));
    }

    [Test]
    public async Task UpdateQuantity_PendingOrder_ChangesQuantityWithoutTouchingStock()
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Pending, quantity: 2, stock: 10);

        using var response = await _owner.Client.PutAsJsonAsync(
            $"/api/orders/{orderId}/quantity", new UpdateOrderQuantityDto { Quantity = 5 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await TestDatabase.GetOrderAsync(orderId)).Quantity, Is.EqualTo(5));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(10));
    }

    [Test]
    public async Task UpdateQuantity_PendingOrder_MoreThanIsInStock_Returns409()
    {
        var (orderId, _) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Pending, quantity: 2, stock: 4);

        using var response = await _owner.Client.PutAsJsonAsync(
            $"/api/orders/{orderId}/quantity", new UpdateOrderQuantityDto { Quantity = 5 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await TestDatabase.GetOrderAsync(orderId)).Quantity, Is.EqualTo(2));
    }

    [TestCase(5, 5)]
    [TestCase(1, 9)]
    public async Task UpdateQuantity_ConfirmedOrder_AdjustsStockByTheDifference(int newQuantity, int expectedStock)
    {
        // Confirmed with 2 units reserved, 8 left on the shelf.
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Confirmed, quantity: 2, stock: 8);

        using var response = await _owner.Client.PutAsJsonAsync(
            $"/api/orders/{orderId}/quantity", new UpdateOrderQuantityDto { Quantity = newQuantity });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(expectedStock));
    }

    [Test]
    public async Task UpdateQuantity_ConfirmedOrder_IncreaseBeyondStock_Returns409AndChangesNothing()
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, OrderStatus.Confirmed, quantity: 2, stock: 1);

        using var response = await _owner.Client.PutAsJsonAsync(
            $"/api/orders/{orderId}/quantity", new UpdateOrderQuantityDto { Quantity = 5 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await TestDatabase.GetOrderAsync(orderId)).Quantity, Is.EqualTo(2));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(1));
    }

    [TestCase(OrderStatus.Failed)]
    [TestCase(OrderStatus.Cancelled)]
    public async Task UpdateQuantity_RejectedOrCancelledOrder_Returns409(OrderStatus status)
    {
        var (orderId, variantId) = await SeedOrderWithVariantAsync(_owner.UserId, status, quantity: 2, stock: 10);

        using var response = await _owner.Client.PutAsJsonAsync(
            $"/api/orders/{orderId}/quantity", new UpdateOrderQuantityDto { Quantity = 3 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await TestDatabase.GetStockAsync(variantId), Is.EqualTo(10));
    }
}
