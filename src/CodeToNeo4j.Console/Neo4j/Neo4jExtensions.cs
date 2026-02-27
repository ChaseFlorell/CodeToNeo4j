using Neo4j.Driver;
using Polly;
using Polly.Retry;

namespace CodeToNeo4j.Console.Neo4j;

public static class Neo4jExtensions
{
    private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Neo4jException>(ex => ex.IsRetriable),
            MaxRetryAttempts = 5,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1)
        })
        .Build();

    public static async Task<IResultCursor> RunAsyncWithRetry(this IAsyncSession session, string query, object? parameters = null) =>
        await Pipeline.ExecuteAsync(async ct => await session.RunAsync(query, parameters));

    public static async Task<IResultCursor> RunAsyncWithRetry(this IAsyncQueryRunner runner, string query, object? parameters = null) =>
        await Pipeline.ExecuteAsync(async ct => await runner.RunAsync(query, parameters));
}