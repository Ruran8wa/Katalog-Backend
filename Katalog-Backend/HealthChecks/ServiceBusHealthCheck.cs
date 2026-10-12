using Azure.Messaging.ServiceBus.Administration;
using Katalog_Backend.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Katalog_Backend.HealthChecks;

/// <summary>
/// Verifies the Azure Service Bus namespace is reachable by fetching the
/// runtime properties of the configured topic.
///
/// The check is skipped (Healthy) when no connection string is configured so
/// that local development without Service Bus still starts cleanly.
/// </summary>
public class ServiceBusHealthCheck(IOptions<AzureServiceBusOptions> options) : IHealthCheck
{
    private readonly AzureServiceBusOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            return HealthCheckResult.Healthy("Azure Service Bus is not configured — skipped.");

        try
        {
            var connectionString = _options.ConnectionString.Trim('\"', '\'').Trim();
            var topicName = _options.TopicName.Trim('\"', '\'').Trim();

            var admin = new ServiceBusAdministrationClient(connectionString);
            await admin.GetTopicRuntimePropertiesAsync(topicName, cancellationToken);
            return HealthCheckResult.Healthy("Azure Service Bus is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Azure Service Bus is unreachable.",
                ex,
                new Dictionary<string, object> { ["topic"] = _options.TopicName });
        }
    }
}
