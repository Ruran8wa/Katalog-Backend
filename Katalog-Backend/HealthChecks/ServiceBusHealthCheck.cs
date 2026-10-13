using Azure.Messaging.ServiceBus.Administration;
using Katalog_Backend.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Katalog_Backend.HealthChecks;

/// <summary>
/// Verifies the Azure Service Bus namespace is reachable by fetching the
/// runtime properties of the configured queue.
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
            var queueName = _options.QueueName.Trim('\"', '\'').Trim();

            // The Azure Service Bus Emulator exposes administration operations over HTTP on port 5300,
            // while AMQP messaging runs on port 5672. Point the administration client
            // at the emulator's management endpoint instead of its AMQP endpoint.
            if (connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = System.Text.RegularExpressions.Regex.Replace(
                    connectionString,
                    @"(Endpoint=sb://[^;:/]+)(?::\d+)?/?(;|$)",
                    "$1:5300$2");
            }

            var admin = new ServiceBusAdministrationClient(connectionString);
            await admin.GetQueueRuntimePropertiesAsync(queueName, cancellationToken);
            return HealthCheckResult.Healthy("Azure Service Bus is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Azure Service Bus is unreachable.",
                ex,
                new Dictionary<string, object> { ["queue"] = _options.QueueName });
        }
    }
}
