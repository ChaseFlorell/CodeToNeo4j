using System.Diagnostics.CodeAnalysis;
using Neo4j.Driver;
using Polly;
using Polly.Retry;

namespace CodeToNeo4j.Neo4j;

[ExcludeFromCodeCoverage(Justification = "Requires live Neo4j IAsyncSession/IAsyncQueryRunner — no unit-testable seam")]
public static class Neo4jExtensions
{
    public static async Task<IResultCursor> RunWithRetry(this IAsyncSession session, string query, object? parameters = null) =>
        await _pipeline.ExecuteAsync(async _ => await session.RunAsync(query, parameters))
            .ConfigureAwait(false);

    public static async Task<IResultCursor> RunWithRetry(this IAsyncQueryRunner runner, string query, object? parameters = null) =>
        await _pipeline.ExecuteAsync(async _ => await runner.RunAsync(query, parameters))
            .ConfigureAwait(false);

    private static readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Neo4jException>(ex => ex.IsRetriable),
            MaxRetryAttempts = 5,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(10)
        })
        .Build();
}
