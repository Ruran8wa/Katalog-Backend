using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Katalog_Backend.Configuration;
using Katalog_Backend.Data;
using Katalog_Backend.Enums;
using Katalog_Backend.Events;
using Katalog_Backend.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Context.Propagation;

namespace Katalog_Backend.BackgroundServices;

public class OrderStockDecrementConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AzureServiceBusOptions _options;
    private readonly ILogger<OrderStockDecrementConsumer> _logger;
    private ServiceBusClient? _client;
    private ServiceBusSessionProcessor? _processor;

    public OrderStockDecrementConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<AzureServiceBusOptions> options,
        ILogger<OrderStockDecrementConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.IsConfigured)
        {
            try
            {
                if (_options.MaxConcurrentSessions < 1 ||
                    _options.MaxConcurrentCallsPerSession < 1 ||
                    _options.SessionIdleTimeoutSeconds < 1)
                {
                    throw new InvalidOperationException(
                        "Azure Service Bus consumer concurrency and idle timeout settings must be greater than zero.");
                }

                _client = new ServiceBusClient(_options.ConnectionString);
                _processor = _client.CreateSessionProcessor(_options.QueueName, new ServiceBusSessionProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentSessions = _options.MaxConcurrentSessions,
                    MaxConcurrentCallsPerSession = _options.MaxConcurrentCallsPerSession,
                    SessionIdleTimeout = TimeSpan.FromSeconds(_options.SessionIdleTimeoutSeconds)
                });
                
                _processor.ProcessMessageAsync += HandleMessageAsync;
                _processor.ProcessErrorAsync += HandleErrorAsync;
                
                await _processor.StartProcessingAsync(cancellationToken);
                _logger.LogInformation(
                    "OrderStockDecrementConsumer started listening on queue '{QueueName}' with {MaxConcurrentSessions} concurrent sessions and {MaxConcurrentCallsPerSession} calls per session.",
                    _options.QueueName,
                    _options.MaxConcurrentSessions,
                    _options.MaxConcurrentCallsPerSession);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Service Bus processor for OrderStockDecrementConsumer.");
            }
        }
        else
        {
            _logger.LogWarning("Azure Service Bus is not configured. Order stock decrement consumer will not start listening.");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ServiceBusProcessor runs in background once started
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(ProcessSessionMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        var parentContext = Propagators.DefaultTextMapPropagator.Extract(
            default,
            args.Message.ApplicationProperties,
            static (properties, key) =>
                properties.TryGetValue(key, out var value)
                    ? new[] { value?.ToString() ?? string.Empty }
                    : Array.Empty<string>());

        Activity? stockActivity = null;
        using var activity = Telemetry.ActivitySource.StartActivity(
            "servicebus process OrderPlaced",
            ActivityKind.Consumer,
            parentContext.ActivityContext);
        activity?.SetTag("messaging.system", "azure_service_bus");
        activity?.SetTag("messaging.destination.name", _options.QueueName);
        activity?.SetTag("messaging.message.id", args.Message.MessageId);
        using var traceScope = Telemetry.BeginTraceScope(_logger);
        try
        {
            _logger.LogInformation("Received message with ID: {MessageId}, Subject: {Subject}", args.Message.MessageId, args.Message.Subject);

            var orderPlacedEvent = JsonSerializer.Deserialize<OrderPlacedEvent>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (orderPlacedEvent == null)
            {
                _logger.LogWarning("Could not deserialize message body to OrderPlacedEvent. Dead-lettering message.");
                await args.DeadLetterMessageAsync(args.Message, "DeserializationError", "Message body could not be parsed to OrderPlacedEvent.");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            stockActivity = Telemetry.ActivitySource.StartActivity(
                "order stock check and decrement",
                ActivityKind.Internal);
            stockActivity?.SetTag("order.id", orderPlacedEvent.OrderId);
            var processingOutcome = "rejected";
            var rejectionReason = "unknown";

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();

                var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == orderPlacedEvent.OrderId);
                if (order == null)
                {
                    stockActivity?.SetTag("order.outcome", "not_found");
                    rejectionReason = "order_not_found";
                    _logger.LogWarning("Order with ID {OrderId} not found. Skipping stock decrement.", orderPlacedEvent.OrderId);
                    return;
                }

                // Idempotency: only process if the order is still Pending
                if (order.Status != OrderStatus.Pending)
                {
                    stockActivity?.SetTag("order.outcome", "already_processed");
                    rejectionReason = "already_processed";
                    _logger.LogInformation("Order {OrderId} is already in state '{Status}'. Skipping duplicate processing.", order.Id, order.Status);
                    return;
                }

                var variant = await context.Variants.FirstOrDefaultAsync(v => v.Id == orderPlacedEvent.VariantId);
                if (variant == null)
                {
                    stockActivity?.SetTag("order.outcome", "variant_not_found");
                    rejectionReason = "variant_not_found";
                    _logger.LogWarning("Variant with ID {VariantId} not found for Order {OrderId}. Marking order as Failed.", orderPlacedEvent.VariantId, order.Id);
                    order.Status = OrderStatus.Failed;
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    RecordProcessedOrder(order.CreatedAt, processingOutcome, rejectionReason);
                    return;
                }

                if (variant.Quantity >= orderPlacedEvent.Quantity)
                {
                    variant.Quantity -= orderPlacedEvent.Quantity;
                    order.Status = OrderStatus.Confirmed;
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    stockActivity?.SetTag("variant.sku", variant.Sku);
                    stockActivity?.SetTag("order.outcome", "confirmed");
                    processingOutcome = "confirmed";
                    rejectionReason = "none";
                    RecordProcessedOrder(order.CreatedAt, processingOutcome, rejectionReason);
                    _logger.LogInformation(
                        "Stock decremented by {Quantity} for Variant {VariantId}. Order {OrderId} confirmed. Remaining stock: {RemainingStock}",
                        orderPlacedEvent.Quantity,
                        variant.Id,
                        order.Id,
                        variant.Quantity);
                }
                else
                {
                    // Race condition: another order took the remaining stock
                    order.Status = OrderStatus.Failed;
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    stockActivity?.SetTag("variant.sku", variant.Sku);
                    stockActivity?.SetTag("order.outcome", "insufficient_stock");
                    rejectionReason = "insufficient_stock";
                    RecordProcessedOrder(order.CreatedAt, processingOutcome, rejectionReason);
                    _logger.LogWarning(
                        "Insufficient stock for Variant {VariantId} during Order {OrderId} processing. Requested: {Requested}, Available: {Available}. Order marked as Failed.",
                        variant.Id,
                        order.Id,
                        orderPlacedEvent.Quantity,
                        variant.Quantity);
                }
            });

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            stockActivity?.SetTag("order.outcome", "error");
            stockActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error processing Azure Service Bus message {MessageId}. Message will be abandoned.", args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message);
        }
        finally
        {
            stockActivity?.Dispose();
        }
    }

    private static void RecordProcessedOrder(DateTime createdAt, string outcome, string rejectionReason)
    {
        var tags = new TagList
        {
            { "outcome", outcome },
            { "rejection.reason", rejectionReason }
        };
        Telemetry.OrdersProcessed.Add(1, tags);
        Telemetry.OrderProcessingDuration.Record(
            Math.Max(0, (DateTime.UtcNow - createdAt.ToUniversalTime()).TotalMilliseconds));
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Azure Service Bus processor encountered an error. Source: {ErrorSource}, Entity: {EntityPath}",
            args.ErrorSource,
            args.EntityPath);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            try
            {
                await _processor.StopProcessingAsync(cancellationToken);
            }
            finally
            {
                await _processor.DisposeAsync();
            }
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
