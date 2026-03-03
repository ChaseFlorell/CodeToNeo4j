# Lessons Learned

## Multi-edit Tool usage
- When using `multi_edit`, be extremely careful not to accidentally delete methods or logic that were not intended to be changed. Always verify the resulting file structure.
- In this refactoring, I accidentally removed `GetChangedFiles` during a large replacement. I had to restore it in a subsequent step.

## SRP in SolutionProcessor
- Large orchestration classes like `SolutionProcessor` tend to accumulate responsibilities (File discovery, Git diffing, Dependency extraction, DB initialization).
- Extracting these into services (e.g., `ISolutionFileDiscoveryService`, `IDependencyIngestor`) makes the main orchestration logic much clearer.
- Using `TextDocument` instead of `Document` allows for better interoperability between regular documents and additional documents in Roslyn.

## Roslyn Interoperability
- When handling different file types in a Roslyn solution, prefer `TextDocument` over `Document` in interfaces that need to handle `AdditionalDocument`, `AnalyzerConfigDocument`, or just regular files from disk.
- If syntax tree operations are required (e.g., in `CSharpHandler`), cast the `TextDocument` to `Document` locally. This prevents non-C# documents (which are still `TextDocument`s) from being nulled out by an `as Document` cast at a higher level of abstraction.

## Async Performance and Task Selection
- Use `Task` and `Task<T>` as the default choice for most asynchronous methods, especially for I/O-bound operations (like Neo4j or Git calls) and long-running tasks.
- `ValueTask` and `ValueTask<T>` should be reserved for high-performance "hot paths" where an operation frequently completes synchronously (e.g., from an in-memory cache) and the overhead of a `Task` allocation is a measurable bottleneck.
- For library or non-UI code, always use `ConfigureAwait(false)` on all awaits to prevent unnecessary thread marshalling back to the original synchronization context, which improves performance and avoids potential deadlocks.
- When multiple tasks need to be awaited together, use `Task.WhenAll`. If using `ValueTask`, they must be converted to `Task` via `.AsTask()` before being passed to `Task.WhenAll`.
- A `ValueTask` should only be awaited once. If you need to await it multiple times or store it, convert it to a `Task`.
- Ensure the main entry point (`Main`) returns `Task<int>` or `int`, as `ValueTask<int>` is not a valid entry point signature in all C# versions/environments.

## Console Output and Progress
- Use `Console.Write("\r...")` to return the cursor to the beginning of the line for "in-place" single-line updates.
- Branch progress reporting behavior based on the `LogLevel` passed to the `IProgressService`.
- For `LogLevel.Information`, use single-line updates; for `LogLevel.Debug` or lower, use multi-line updates; for `LogLevel.Warning` or higher, suppress output entirely.
- Standard loggers (`Microsoft.Extensions.Logging`) typically append newlines to every log entry, which prevents single-line updates. A custom `ILogger` implementation can be used to manage output formatting and suppression of specific logs (like "flushing symbols") during progress reporting to avoid interfering with the single-line display.
- For fatal errors, wrap the main entry point in a try-catch and use `Console.Error.WriteLine(ex.ToString())` for error reporting before exiting with the exception's `HResult`.
- Ensure the main entry point (`Main`) returns `Task<int>` or `int`, as `ValueTask<int>` is not a valid entry point signature in all C# versions/environments.
- For performance-critical loops (like processing thousands of files), leverage `Task.WhenAll` or `Parallel.ForEachAsync` to process work in parallel when steps are independent.
- Use chunked parallelism or limit the degree of parallelism (e.g., `MaxDegreeOfParallelism`) to avoid overwhelming resources (DB connections, memory) while still gaining performance from concurrency.
- When parallelizing, ensure shared resources are either thread-safe or refactored to be local to the parallel task.

## .NET Global Tool Packaging
- For .NET global tools, multi-targeting (e.g., `net10.0;net9.0;net8.0`) improves compatibility with older `dotnet` CLI versions that may not yet fully support the latest framework (like `net10.0`) during tool installation.
- This ensures `DotnetToolSettings.xml` is present in the expected framework-specific folders (e.g., `tools/net8.0/any/`) that older installers can recognize.
- Update `PACKAGE_README.md` to reflect the broader framework support when adding new targets.

## Neo4j Granularity and Progress Reporting
- For interactive CLI tools, providing immediate, granular progress feedback (e.g., every file processed) is often more important to the user experience than the performance gains of batching.
- While batching Neo4j writes into transactions (e.g., 200 files at once) is significantly faster, it can make progress reporting appear "stuck" or "sporadic" until the entire batch finishes.
- Reverting to per-file writes (while still using parallel processing for CPU-bound tasks) allows for smoother and more responsive "one file at a time" progress updates.
- Parallelizing independent file processing using `Task.WhenAll` can still provide significant performance benefits, even with individual database writes, provided the database handles the concurrency well.
- Using a callback approach (e.g., `onProcessed` delegate) in parallel processing loops allows for immediate, thread-safe progress reporting and database updates as each task completes.
- When processing large solutions with Roslyn, avoid launching thousands of parallel tasks for all files at once (e.g., via `Task.WhenAll` on a large collection). This can lead to `OutOfMemoryException` because each task may hold onto Roslyn documents, semantic models, and other memory-intensive objects simultaneously.
- Instead, use a sequential `foreach` loop for top-level file processing to keep memory usage stable, while still using parallelism for smaller, lower-level operations like concurrent database writes (e.g., flushing symbols and relationships in a single transaction).
- Memory management is as important as throughput; batching can be used to balance database performance with local resource constraints.
