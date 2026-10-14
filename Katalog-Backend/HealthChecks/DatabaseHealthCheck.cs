using Katalog_Backend.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Katalog_Backend.HealthChecks;

/// <summary>
/// Verifies database connectivity by executing a lightweight CanConnectAsync check.
/// </summary>
public class DatabaseHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Cannot connect to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
