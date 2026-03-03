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

## Performance and Architecture Patterns for Large Scale Code Analysis
- **Producer-Consumer Pattern**: For processing large solutions with many files, decoupling the analysis (CPU-bound, Roslyn) from the database ingestion (I/O-bound, Neo4j) using `System.Threading.Channels` significantly improves throughput. This allows Roslyn to process files as fast as possible in parallel, while a single consumer task batches and flushes results to the database optimally.
- **Batched Git Metadata**: Fetching file history (e.g., `git log`) for thousands of files individually is extremely slow due to process startup overhead. Instead, pre-fetch metadata for all files in a single `git log --name-only` pass and cache it.
- **Lazy Roslyn Loading**: To minimize memory pressure, avoid loading all `Compilation` and `Document` objects for a solution up front. Store only `ProjectId` and `DocumentId`, and load the required Roslyn objects lazily within the parallel processing loop. This keeps the number of active compilations proportional to the `MaxDegreeOfParallelism`.
- **Database Batching with UNWIND**: When using Neo4j, always prefer batching multiple operations (e.g., `UpsertFile`, `DeletePriorSymbols`) into a single transaction using the `UNWIND` Cypher pattern. This reduces session overhead and network roundtrips.
- **Streaming I/O**: For calculating file hashes (e.g., SHA256), use streaming APIs like `File.OpenRead` and `HashDataAsync` instead of `ReadAllBytesAsync` to prevent large memory allocations, especially for large source files.
- **Lock-Free Batching**: Using a dedicated consumer task for database flushes eliminates the need for complex locking mechanisms in the parallel processing loop, reducing contention and simplifying thread-safety logic.
