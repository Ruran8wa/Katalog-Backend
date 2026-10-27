using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Katalog_Backend.BackgroundServices.Interfaces;
using Katalog_Backend.Configuration;
using Katalog_Backend.Events;
using Katalog_Backend.Observability;
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
            var handler = scope.ServiceProvider.GetRequiredService<IOrderPlacedHandler>();
            await handler.HandleAsync(orderPlacedEvent, args.CancellationToken);

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
        using var activity = Telemetry.ActivitySource.StartActivity(
            "servicebus process error",
            ActivityKind.Consumer);
        using var traceScope = Telemetry.BeginTraceScope(_logger);
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
