using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Katalog_Backend.Configuration;
using Katalog_Backend.Data;
using Katalog_Backend.Enums;
using Katalog_Backend.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Katalog_Backend.BackgroundServices;

public class OrderStockDecrementConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AzureServiceBusOptions _options;
    private readonly ILogger<OrderStockDecrementConsumer> _logger;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

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
                _client = new ServiceBusClient(_options.ConnectionString);
                _processor = _client.CreateProcessor(_options.TopicName, _options.SubscriptionName, new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = 1
                });

                _processor.ProcessMessageAsync += HandleMessageAsync;
                _processor.ProcessErrorAsync += HandleErrorAsync;

                await _processor.StartProcessingAsync(cancellationToken);
                _logger.LogInformation(
                    "OrderStockDecrementConsumer started listening on Topic '{TopicName}', Subscription '{SubscriptionName}'.",
                    _options.TopicName,
                    _options.SubscriptionName);
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

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        _logger.LogInformation("Received message with ID: {MessageId}, Subject: {Subject}", args.Message.MessageId, args.Message.Subject);

        try
        {
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

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();

                var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == orderPlacedEvent.OrderId);
                if (order == null)
                {
                    _logger.LogWarning("Order with ID {OrderId} not found. Skipping stock decrement.", orderPlacedEvent.OrderId);
                    return;
                }

                // Idempotency: only process if the order is still Pending
                if (order.Status != OrderStatus.Pending)
                {
                    _logger.LogInformation("Order {OrderId} is already in state '{Status}'. Skipping duplicate processing.", order.Id, order.Status);
                    return;
                }

                var variant = await context.Variants.FirstOrDefaultAsync(v => v.Id == orderPlacedEvent.VariantId);
                if (variant == null)
                {
                    _logger.LogWarning("Variant with ID {VariantId} not found for Order {OrderId}. Marking order as Failed.", orderPlacedEvent.VariantId, order.Id);
                    order.Status = OrderStatus.Failed;
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return;
                }

                if (variant.Quantity >= orderPlacedEvent.Quantity)
                {
                    variant.Quantity -= orderPlacedEvent.Quantity;
                    order.Status = OrderStatus.Confirmed;
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

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
            _logger.LogError(ex, "Error processing Azure Service Bus message {MessageId}. Message will be abandoned.", args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message);
        }
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
