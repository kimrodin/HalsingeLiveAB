using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Booking.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!connection.IsConnected)
            return HealthCheckResult.Unhealthy("Redis is not connected.");

        await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
