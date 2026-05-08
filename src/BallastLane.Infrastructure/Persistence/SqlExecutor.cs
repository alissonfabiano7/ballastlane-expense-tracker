using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace BallastLane.Infrastructure.Persistence;

public sealed class SqlExecutor
{
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        -2,    // Timeout
        20,    // The instance of SQL Server you attempted to connect to does not support encryption
        53,    // Network path not found
        64,    // Specified network name no longer available
        121,   // Semaphore timeout
        233,   // No process is on the other end of the pipe
        1205,  // Deadlock victim
        4060,  // Cannot open database
        10053, // Connection aborted
        10054, // Connection reset
        10060, // Connection timeout
        11001, // DNS lookup failed
        40197, // Service error
        40501, // Service busy
        40613, // Database unavailable
        49918, // Cannot process request — not enough resources
        49919, // Cannot process create/update request
        49920, // Cannot process request — too busy
    ];

    private readonly string _connectionString;
    private readonly ResiliencePipeline _pipeline;

    public SqlExecutor(string connectionString, ILogger<SqlExecutor> logger)
    {
        _connectionString = connectionString;
        _pipeline = BuildPipeline(logger);
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(async ct =>
        {
            SqlConnection connection = new(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return connection;
        }, cancellationToken);
    }

    public async Task<int> ExecuteNonQueryAsync(
        string commandText,
        Action<SqlCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(async ct =>
        {
            await using SqlConnection connection = new(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            configureCommand(command);
            return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public async Task<TResult?> ExecuteScalarAsync<TResult>(
        string commandText,
        Action<SqlCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(async ct =>
        {
            await using SqlConnection connection = new(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            configureCommand(command);
            object? result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is null or DBNull ? default : (TResult)result;
        }, cancellationToken);
    }

    public async Task<TResult> ExecuteReaderAsync<TResult>(
        string commandText,
        Action<SqlCommand> configureCommand,
        Func<SqlDataReader, CancellationToken, Task<TResult>> read,
        CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(async ct =>
        {
            await using SqlConnection connection = new(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = commandText;
            configureCommand(command);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await read(reader, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public static ResiliencePipeline BuildPipeline(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(IsTransient)
                    .Handle<TimeoutException>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient database failure on attempt {Attempt}; retrying after {Delay}ms.",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    public static bool IsTransient(SqlException exception)
    {
        foreach (SqlError error in exception.Errors)
        {
            if (TransientErrorNumbers.Contains(error.Number))
            {
                return true;
            }
        }
        return false;
    }
}
