using BallastLane.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Shouldly;

namespace BallastLane.Infrastructure.Tests.Persistence;

public class SqlExecutorPipelineTests
{
    [Fact]
    public async Task Pipeline_retries_transient_failures_and_eventually_succeeds()
    {
        ResiliencePipeline pipeline = SqlExecutor.BuildPipeline(NullLogger.Instance);
        int attempts = 0;

        await pipeline.ExecuteAsync(async _ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new TimeoutException("Transient.");
            }
            await Task.CompletedTask;
        }, CancellationToken.None);

        attempts.ShouldBe(3);
    }

    [Fact]
    public async Task Pipeline_does_not_retry_non_transient_exceptions()
    {
        ResiliencePipeline pipeline = SqlExecutor.BuildPipeline(NullLogger.Instance);
        int attempts = 0;

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await pipeline.ExecuteAsync(async _ =>
            {
                attempts++;
                throw new InvalidOperationException("Permanent.");
#pragma warning disable CS0162 // Unreachable code detected (unreachable by design)
                await Task.CompletedTask;
#pragma warning restore CS0162
            }, CancellationToken.None);
        });

        attempts.ShouldBe(1);
        error.Message.ShouldBe("Permanent.");
    }
}
