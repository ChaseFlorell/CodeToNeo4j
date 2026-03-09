using Neo4j.Driver;
using Polly;
using Polly.Retry;

namespace CodeToNeo4j.Neo4j;

public static class Neo4jExtensions
{
    private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Neo4jException>(ex => ex.IsRetriable),
            MaxRetryAttempts = 5,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(10)
        })
        .Build();

    public static async Task<IResultCursor> RunWithRetry(this IAsyncSession session, string query, object? parameters = null) =>
        await Pipeline.ExecuteAsync(async _ => await session.RunAsync(query, parameters))
            .ConfigureAwait(false);

    public static async Task<IResultCursor> RunWithRetry(this IAsyncQueryRunner runner, string query, object? parameters = null) =>
        await Pipeline.ExecuteAsync(async _ => await runner.RunAsync(query, parameters))
            .ConfigureAwait(false);
}