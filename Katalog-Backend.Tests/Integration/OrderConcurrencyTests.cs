using Katalog_Backend.BackgroundServices;
using Katalog_Backend.Enums;
using Katalog_Backend.Events;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Repositories;
using Katalog_Backend.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Integration;

// Two contexts stand in for two requests: one reads the order, the other changes it, then the stale one saves.
[TestFixture]
[NonParallelizable]
public sealed class OrderConcurrencyTests
{
    private string _userId = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        TestDatabase.EnsureMigrated();
        _userId = (await TestDatabase.SeedUserAsync()).Id;
    }

    [Test]
    public async Task CancelReadAsPending_AfterTheConsumerConfirmedIt_ConflictsAndTheOrderStaysConfirmed()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 10);
        var seeded = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3);

        await using var apiContext = TestDatabase.CreateContext();
        var repository = new OrderRepository(apiContext);
        var staleOrder = (await repository.GetByIdAsync(seeded.Id))!;

        await using (var consumerContext = TestDatabase.CreateContext())
        {
            await new OrderPlacedHandler(consumerContext, NullLogger<OrderPlacedHandler>.Instance).HandleAsync(
                new OrderPlacedEvent(seeded.Id, seeded.UserId, seeded.VariantId, seeded.Quantity, seeded.CreatedAt));
        }

        staleOrder.Status = OrderStatus.Cancelled;
        Assert.ThrowsAsync<OrderConcurrencyConflictException>(
            () => repository.SaveWithStockChangeAsync(staleOrder, stockDelta: 0));

        Assert.That((await TestDatabase.GetOrderAsync(seeded.Id)).Status, Is.EqualTo(OrderStatus.Confirmed));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(7));
    }

    [Test]
    public async Task TwoCancellationsOfTheSameConfirmedOrder_ReleaseItsStockOnlyOnce()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 7);
        var seeded = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3, OrderStatus.Confirmed);

        await using var firstContext = TestDatabase.CreateContext();
        await using var secondContext = TestDatabase.CreateContext();
        var first = new OrderRepository(firstContext);
        var second = new OrderRepository(secondContext);
        var firstRead = (await first.GetByIdAsync(seeded.Id))!;
        var secondRead = (await second.GetByIdAsync(seeded.Id))!;

        firstRead.Status = OrderStatus.Cancelled;
        await first.SaveWithStockChangeAsync(firstRead, stockDelta: 3);
        secondRead.Status = OrderStatus.Cancelled;
        Assert.ThrowsAsync<OrderConcurrencyConflictException>(
            () => second.SaveWithStockChangeAsync(secondRead, stockDelta: 3));

        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(10));
    }

    [Test]
    public async Task QuantityChangeReadAsPending_AfterTheConsumerConfirmedIt_ConflictsAndChangesNothing()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 10);
        var seeded = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3);

        await using var apiContext = TestDatabase.CreateContext();
        var repository = new OrderRepository(apiContext);
        var staleOrder = (await repository.GetByIdAsync(seeded.Id))!;

        await using (var consumerContext = TestDatabase.CreateContext())
        {
            await new OrderPlacedHandler(consumerContext, NullLogger<OrderPlacedHandler>.Instance).HandleAsync(
                new OrderPlacedEvent(seeded.Id, seeded.UserId, seeded.VariantId, seeded.Quantity, seeded.CreatedAt));
        }

        // Read as Pending, so the service would change no stock.
        staleOrder.Quantity = 5;
        Assert.ThrowsAsync<OrderConcurrencyConflictException>(
            () => repository.SaveWithStockChangeAsync(staleOrder, stockDelta: 0));

        var stored = await TestDatabase.GetOrderAsync(seeded.Id);
        Assert.That(stored.Quantity, Is.EqualTo(3));
        Assert.That(stored.Status, Is.EqualTo(OrderStatus.Confirmed));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(7));
    }

    [Test]
    public async Task TwoDeletesOfTheSameConfirmedOrder_TheSecondConflictsAndStockIsReleasedOnce()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 7);
        var seeded = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3, OrderStatus.Confirmed);

        await using var firstContext = TestDatabase.CreateContext();
        await using var secondContext = TestDatabase.CreateContext();
        var first = new OrderRepository(firstContext);
        var second = new OrderRepository(secondContext);
        var firstRead = (await first.GetByIdAsync(seeded.Id))!;
        var secondRead = (await second.GetByIdAsync(seeded.Id))!;

        await first.DeleteAsync(firstRead, releasedStock: 3);
        Assert.ThrowsAsync<OrderConcurrencyConflictException>(() => second.DeleteAsync(secondRead, releasedStock: 3));

        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(10));
    }
}
