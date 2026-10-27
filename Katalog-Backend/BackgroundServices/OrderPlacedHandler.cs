using System.Diagnostics;
using Katalog_Backend.BackgroundServices.Interfaces;
using Katalog_Backend.Data;
using Katalog_Backend.Enums;
using Katalog_Backend.Events;
using Katalog_Backend.Observability;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.BackgroundServices;

public class OrderPlacedHandler : IOrderPlacedHandler
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderPlacedHandler> _logger;

    public OrderPlacedHandler(ApplicationDbContext context, ILogger<OrderPlacedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderProcessingResult> HandleAsync(
        OrderPlacedEvent orderPlaced,
        CancellationToken cancellationToken = default)
    {
        using var stockActivity = Telemetry.ActivitySource.StartActivity(
            "order stock check and decrement",
            ActivityKind.Internal);
        stockActivity?.SetTag("order.id", orderPlaced.OrderId);

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            var result = await strategy.ExecuteAsync(
                () => ProcessAsync(orderPlaced, stockActivity, cancellationToken));
            stockActivity?.SetTag("order.outcome", result.Reason ?? "confirmed");
            return result;
        }
        catch (Exception ex)
        {
            stockActivity?.SetTag("order.outcome", "error");
            stockActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<OrderProcessingResult> ProcessAsync(
        OrderPlacedEvent orderPlaced,
        Activity? stockActivity,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderPlaced.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order with ID {OrderId} not found. Skipping stock decrement.", orderPlaced.OrderId);
            return new OrderProcessingResult(OrderProcessingOutcome.Skipped, "order_not_found");
        }

        // Idempotency: only process if the order is still Pending
        if (order.Status != OrderStatus.Pending)
        {
            _logger.LogInformation("Order {OrderId} is already {Status}. Skipping duplicate processing.", order.Id, order.Status.ToDomainName());
            return new OrderProcessingResult(OrderProcessingOutcome.Skipped, "already_processed");
        }

        var variant = await _context.Variants.FirstOrDefaultAsync(v => v.Id == orderPlaced.VariantId, cancellationToken);
        if (variant == null)
        {
            _logger.LogWarning("Variant with ID {VariantId} not found for Order {OrderId}. Order rejected.", orderPlaced.VariantId, order.Id);
            order.Status = OrderStatus.Failed;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Processed(order.CreatedAt, order.Status, "variant_not_found");
        }

        stockActivity?.SetTag("variant.sku", variant.Sku);
        if (variant.Quantity >= orderPlaced.Quantity)
        {
            variant.Quantity -= orderPlaced.Quantity;
            order.Status = OrderStatus.Confirmed;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Stock decremented by {Quantity} for Variant {VariantId}. Order {OrderId} confirmed. Remaining stock: {RemainingStock}",
                orderPlaced.Quantity,
                variant.Id,
                order.Id,
                variant.Quantity);
            return Processed(order.CreatedAt, order.Status, rejectionReason: null);
        }

        // Race condition: another order took the remaining stock
        order.Status = OrderStatus.Failed;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogWarning(
            "Insufficient stock for Variant {VariantId} during Order {OrderId} processing. Requested: {Requested}, Available: {Available}. Order rejected.",
            variant.Id,
            order.Id,
            orderPlaced.Quantity,
            variant.Quantity);
        return Processed(order.CreatedAt, order.Status, "insufficient_stock");
    }

    private static OrderProcessingResult Processed(DateTime createdAt, OrderStatus finalStatus, string? rejectionReason)
    {
        var tags = new TagList
        {
            { "outcome", finalStatus.ToDomainName() },
            { "rejection.reason", rejectionReason ?? "none" }
        };
        Telemetry.OrdersProcessed.Add(1, tags);
        Telemetry.OrderProcessingDuration.Record(
            Math.Max(0, (DateTime.UtcNow - createdAt.ToUniversalTime()).TotalMilliseconds));
        var outcome = finalStatus == OrderStatus.Confirmed
            ? OrderProcessingOutcome.Confirmed
            : OrderProcessingOutcome.Rejected;
        return new OrderProcessingResult(outcome, rejectionReason);
    }
}
