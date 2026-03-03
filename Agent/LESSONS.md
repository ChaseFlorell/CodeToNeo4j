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

## Async Performance and Thread Marshalling

## Console Output and Progress with Spectre.Console
- Use `AnsiConsole.Markup` with a carriage return (`\r`) for "in-place" single-line updates, and `AnsiConsole.MarkupLine` for traditional multi-line logging.
- When progress reporting depends on the `LogLevel`, branch the behavior within the `IProgressService` implementation by passing the `minLogLevel` to its constructor.
- For `LogLevel.Information`, use single-line updates; for `LogLevel.Debug` or lower, use multi-line updates; for `LogLevel.Warning` or higher, suppress output entirely.
- Standard loggers (`Microsoft.Extensions.Logging`) typically append newlines to every log entry, which prevents single-line updates.
- If a standard logger is still needed for other parts of the application, a custom `ILogger` implementation can be used to manage output formatting and suppression of specific logs during progress reporting.
- **CRITICAL: Always use `Markup.Escape()` when passing strings (like log messages, file paths, or class names) to `AnsiConsole.Markup` or `AnsiConsole.MarkupLine`. Failure to do so will cause `InvalidOperationException: Unbalanced markup stack` if the input contains brackets (`[` or `]`).**
- **CRITICAL: When using brackets (`[` and `]`) in the format string itself (e.g., `[{eventId.Id}]`), they MUST be escaped by doubling them (`[[` and `]]`) to avoid Spectre.Console misinterpreting them as markup tags. Even if the content inside is a number like `[0]`, it can cause an unbalanced stack if not doubled.**
- For fatal errors, wrap the main entry point in a try-catch and use `AnsiConsole.WriteException` for rich error reporting before exiting with a non-zero code.
- For performance-critical loops (like processing thousands of files), leverage `Task.WhenAll` to process work in parallel when steps are independent.
- Use chunked parallelism (e.g., chunks of `batchSize`) to avoid overwhelming resources (DB connections, memory) while still gaining performance from concurrency.
- When parallelizing, ensure shared resources are either thread-safe or refactored to be local to the parallel task (e.g., returning results instead of populating a shared buffer).
- For library or non-UI code, always use `ConfigureAwait(false)` on all awaits to prevent unnecessary thread marshalling back to the original synchronization context, which improves performance and avoids potential deadlocks.

## .NET Global Tool Packaging
- For .NET global tools, multi-targeting (e.g., `net10.0;net9.0;net8.0`) improves compatibility with older `dotnet` CLI versions that may not yet fully support the latest framework (like `net10.0`) during tool installation.
- This ensures `DotnetToolSettings.xml` is present in the expected framework-specific folders (e.g., `tools/net8.0/any/`) that older installers can recognize.
- Update `PACKAGE_README.md` to reflect the broader framework support when adding new targets.
