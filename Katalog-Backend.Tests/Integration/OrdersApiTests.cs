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
}
