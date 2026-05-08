using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BallastLane.Api.Tests.TestHost;

internal sealed class StubHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy("stub"));
}
