namespace Katalog_Backend.Configuration;

public class AzureServiceBusOptions
{
    public const string SectionName = "AzureServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "orders-queue";
    public int MaxConcurrentSessions { get; set; } = 1;
    public int MaxConcurrentCallsPerSession { get; set; } = 1;
    public int SessionIdleTimeoutSeconds { get; set; } = 1;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        !ConnectionString.Contains("YOUR_SERVICE_BUS_CONNECTION_STRING", StringComparison.OrdinalIgnoreCase) &&
        !ConnectionString.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
