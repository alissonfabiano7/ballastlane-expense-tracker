using BallastLane.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BallastLane.Infrastructure.HealthChecks;

public sealed class SqlServerHealthCheck(IOptions<SqlSettings> options) : IHealthCheck
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using SqlConnection connection = new(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            command.CommandTimeout = 5;
            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return result is 1
                ? HealthCheckResult.Healthy("SQL Server connection is reachable.")
                : HealthCheckResult.Unhealthy("SELECT 1 returned an unexpected result.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server connection failed.", ex);
        }
    }
}
