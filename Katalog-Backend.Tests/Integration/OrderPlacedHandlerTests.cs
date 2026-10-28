using Katalog_Backend.BackgroundServices;
using Katalog_Backend.BackgroundServices.Interfaces;
using Katalog_Backend.Enums;
using Katalog_Backend.Events;
using Katalog_Backend.Models;
using Katalog_Backend.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Katalog_Backend.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class OrderPlacedHandlerTests
{
    private string _userId = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        TestDatabase.EnsureMigrated();
        _userId = (await TestDatabase.SeedUserAsync()).Id;
    }

    private static async Task<OrderProcessingResult> HandleAsync(Order order, int? eventQuantity = null)
    {
        await using var context = TestDatabase.CreateContext();
        var handler = new OrderPlacedHandler(context, NullLogger<OrderPlacedHandler>.Instance);
        return await handler.HandleAsync(new OrderPlacedEvent(
            order.Id, order.UserId, order.VariantId, eventQuantity ?? order.Quantity, order.CreatedAt));
    }

    [Test]
    public async Task PendingOrderWithEnoughStock_IsConfirmedAndTakesStock()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 10);
        var order = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3);

        var result = await HandleAsync(order);

        Assert.That(result.Outcome, Is.EqualTo(OrderProcessingOutcome.Confirmed));
        Assert.That((await TestDatabase.GetOrderAsync(order.Id)).Status, Is.EqualTo(OrderStatus.Confirmed));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(7));
    }

    [Test]
    public async Task QuantityChangedWhilePending_TakesTheOrdersCurrentQuantityNotTheMessages()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 10);
        var order = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 5);

        var result = await HandleAsync(order, eventQuantity: 2);

        Assert.That(result.Outcome, Is.EqualTo(OrderProcessingOutcome.Confirmed));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(5));
    }

    [Test]
    public async Task CancelledOrder_IsSkippedAndKeepsItsStatus()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 10);
        var order = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3, OrderStatus.Cancelled);

        var result = await HandleAsync(order);

        Assert.That(result.Outcome, Is.EqualTo(OrderProcessingOutcome.Skipped));
        Assert.That((await TestDatabase.GetOrderAsync(order.Id)).Status, Is.EqualTo(OrderStatus.Cancelled));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(10));
    }

    [Test]
    public async Task NotEnoughStock_RejectsTheOrderAndLeavesStockUntouched()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 2);
        var order = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3);

        var result = await HandleAsync(order);

        Assert.That(result, Is.EqualTo(new OrderProcessingResult(OrderProcessingOutcome.Rejected, "insufficient_stock")));
        Assert.That((await TestDatabase.GetOrderAsync(order.Id)).Status, Is.EqualTo(OrderStatus.Failed));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(2));
    }

    [Test]
    public async Task ManyOrdersForTheSameVariantAtOnce_NeverOversell()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 5);
        var orders = new List<Order>();
        for (var i = 0; i < 12; i++)
        {
            orders.Add(await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 1));
        }

        var results = await Task.WhenAll(orders.Select(order => HandleAsync(order)));

        Assert.That(results.Count(r => r.Outcome == OrderProcessingOutcome.Confirmed), Is.EqualTo(5));
        Assert.That(results.Count(r => r.Outcome == OrderProcessingOutcome.Rejected), Is.EqualTo(7));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(0));
    }

    [Test]
    public async Task OrderCancelledAfterTheConsumerReadIt_StaysCancelledAndKeepsItsStock()
    {
        var variant = await TestDatabase.SeedVariantAsync(quantity: 10);
        var order = await TestDatabase.SeedOrderAsync(_userId, variant.Id, quantity: 3);

        await using var consumerContext = TestDatabase.CreateContext();
        // The consumer has already read the order as Pending...
        await consumerContext.Orders.SingleAsync(o => o.Id == order.Id);
        // ...when a cancellation is saved from another request.
        await using (var apiContext = TestDatabase.CreateContext())
        {
            var cancelled = await apiContext.Orders.SingleAsync(o => o.Id == order.Id);
            cancelled.Status = OrderStatus.Cancelled;
            await apiContext.SaveChangesAsync();
        }

        var handler = new OrderPlacedHandler(consumerContext, NullLogger<OrderPlacedHandler>.Instance);
        var result = await handler.HandleAsync(new OrderPlacedEvent(
            order.Id, order.UserId, order.VariantId, order.Quantity, order.CreatedAt));

        Assert.That(result.Outcome, Is.EqualTo(OrderProcessingOutcome.Skipped));
        Assert.That((await TestDatabase.GetOrderAsync(order.Id)).Status, Is.EqualTo(OrderStatus.Cancelled));
        Assert.That(await TestDatabase.GetStockAsync(variant.Id), Is.EqualTo(10));
    }
}
