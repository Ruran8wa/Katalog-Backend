namespace Katalog_Backend.Configuration;

public class AzureServiceBusOptions
{
    public const string SectionName = "AzureServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string TopicName { get; set; } = "order-placed-topic";
    public string SubscriptionName { get; set; } = "order-stock-worker-sub";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        !ConnectionString.Contains("YOUR_SERVICE_BUS_CONNECTION_STRING", StringComparison.OrdinalIgnoreCase);
}
