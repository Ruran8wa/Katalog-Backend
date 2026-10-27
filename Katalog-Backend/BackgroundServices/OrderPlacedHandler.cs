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
            var result = await ProcessWithConflictRetryAsync(orderPlaced, stockActivity, cancellationToken);
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

    private async Task<OrderProcessingResult> ProcessWithConflictRetryAsync(
        OrderPlacedEvent orderPlaced,
        Activity? stockActivity,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var strategy = _context.Database.CreateExecutionStrategy();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await strategy.ExecuteAsync(
                    () => ProcessAsync(orderPlaced, stockActivity, cancellationToken));
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                // The order changed after it was read (cancelled, or its quantity edited).
                // The transaction rolled back, including the stock decrement; re-read and decide again.
                _logger.LogInformation(
                    "Order {OrderId} changed while it was being processed. Retrying (attempt {Attempt}).",
                    orderPlaced.OrderId,
                    attempt + 1);
                _context.ChangeTracker.Clear();
            }
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

        // Take the quantity from the order row: it may have changed since the message was queued.
        var quantity = order.Quantity;
        var stockTaken = await _context.Variants.TryTakeStockAsync(order.VariantId, quantity, cancellationToken);

        var variant = await _context.Variants
            .Where(v => v.Id == order.VariantId)
            .Select(v => new { v.Sku, v.Quantity })
            .FirstOrDefaultAsync(cancellationToken);
        stockActivity?.SetTag("variant.sku", variant?.Sku);

        if (stockTaken)
        {
            order.Status = OrderStatus.Confirmed;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Stock decremented by {Quantity} for Variant {VariantId}. Order {OrderId} confirmed. Remaining stock: {RemainingStock}",
                quantity,
                order.VariantId,
                order.Id,
                variant?.Quantity);
            return Processed(order.CreatedAt, order.Status, rejectionReason: null);
        }

        order.Status = OrderStatus.Failed;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (variant == null)
        {
            _logger.LogWarning("Variant with ID {VariantId} not found for Order {OrderId}. Order rejected.", order.VariantId, order.Id);
            return Processed(order.CreatedAt, order.Status, "variant_not_found");
        }

        _logger.LogWarning(
            "Insufficient stock for Variant {VariantId} during Order {OrderId} processing. Requested: {Requested}, Available: {Available}. Order rejected.",
            order.VariantId,
            order.Id,
            quantity,
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
