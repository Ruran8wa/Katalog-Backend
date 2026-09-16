using Azure.Messaging.ServiceBus;
using Katalog_Backend.Configuration;
using Katalog_Backend.Repositories.Interfaces;
using Microsoft.Extensions.Options;

namespace Katalog_Backend.BackgroundServices;

public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AzureServiceBusOptions _options;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private ServiceBusClient? _client;
    private ServiceBusSender? _sender;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AzureServiceBusOptions> options,
        ILogger<OutboxPublisherWorker> logger)
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
                _sender = _client.CreateSender(_options.TopicName);
                _logger.LogInformation("OutboxPublisherWorker connected to Azure Service Bus topic '{TopicName}'.", _options.TopicName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Service Bus client in OutboxPublisherWorker.");
            }
        }
        else
        {
            _logger.LogWarning("Azure Service Bus is not configured. Outbox publisher will wait for valid configuration.");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_sender == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

                var messages = await outboxRepo.GetUnprocessedMessagesAsync(batchSize: 20, stoppingToken);
                if (messages.Count > 0)
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            var sbMessage = new ServiceBusMessage(message.Payload)
                            {
                                Subject = message.EventType,
                                MessageId = message.Id.ToString(),
                                ContentType = "application/json"
                            };

                            await _sender.SendMessageAsync(sbMessage, stoppingToken);
                            await outboxRepo.MarkAsProcessedAsync(message.Id, stoppingToken);

                            _logger.LogInformation("Successfully published outbox message {MessageId} ({EventType}) to Azure Service Bus.", message.Id, message.EventType);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to publish outbox message {MessageId} to Azure Service Bus.", message.Id);
                            await outboxRepo.MarkAsFailedAsync(message.Id, ex.Message, stoppingToken);
                        }
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OutboxPublisherWorker execution loop.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_sender != null)
        {
            await _sender.DisposeAsync();
        }
        if (_client != null)
        {
            await _client.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
