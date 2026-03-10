## Lessons Learned

## Parallelized Git Commit Ingestion
- When ingesting large amounts of git history, parallelize the process by batching commits with `--max-count` and `--skip`.
- Use `git rev-list --count` to determine the total number of commits in a range to calculate the required batches.
- Improve `git log` parsing reliability by using custom delimiters (e.g., `|#|`) and a header prefix (e.g., `COMMIT|`) in the `--format` string.
- Decouple commit ingestion from file diffing to allow for more granular control and better performance through parallel execution.
- Support various git range specifications (e.g., `hash1..hash2`) by checking for `..` in the `diff-base` and adjusting the range accordingly (defaulting to `diffBase...HEAD` if not present).

## Class Member Order Rules
- Maintain a strict order for class members from top to bottom:
    1. Constructors
    2. Public members
    3. Internal members
    4. Protected members
    5. Private members
    6. Private static members
    7. Private const members (CRITICAL: These must be at the very bottom of the class)
- This order applies to all new and refactored classes to ensure consistency across the codebase.

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
- For `Parallel.ForEachAsync`, use a thread-safe collection like `ConcurrentBag<T>` or a `Channel<T>` if multiple tasks are adding items simultaneously.
- When ingesting data from multiple parallel sources (like projects in a solution), de-duplicate and sort (e.g., `.DistinctBy().OrderBy()`) the results before passing them to the database to ensure deterministic behavior and minimize transaction conflicts.
- Always check `Project.SupportsCompilation` before attempting to get a `Compilation` in Roslyn to avoid unnecessary overhead or potential exceptions for non-compilable projects (like solution folders or some resource-only projects).
- Be mindful of memory pressure when parallelizing `Project.GetCompilationAsync()`. Using a `MaxDegreeOfParallelism` based on `Environment.ProcessorCount` is generally safer than hardcoded high values like 20, as full compilation is memory-intensive.
- Roslyn caches `Compilation` objects within the same `Solution` instance. Running `GetCompilationAsync()` during an initial dependency ingestion phase can warm up this cache, speeding up subsequent file-level processing that requires semantic information.

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

## Command-Line Design and Data Purging
- When adding powerful administrative commands (like `--purge-data-by-repository-key`), always implement a confirmation prompt to prevent accidental data loss.
- Prefer using `AddValidator` on the `RootCommand` or individual `Option`s to handle argument validation instead of throwing exceptions inside the `SetHandler`. This allows the CLI to display standardized error messages and help text before the application starts executing.
- Use conditional logic in the `RootCommand` handler (or a custom validator) to validate that incompatible options (e.g., `--skip-dependencies` or `--min-accessibility`) are not used alongside specific switches.
- Make mandatory options (like `--sln`) optional in the binder when they are not relevant to the specific mode of operation (e.g., purging by key only), and enforce their presence via validators.
- Implement flexible Cypher queries that can handle both full-scale and filtered (e.g., by extension) data deletions using conditional `WHERE` and `OPTIONAL MATCH` clauses.
- **Neo4j 5.x Parameter Aliasing**: When using parameters (e.g., `$repoKey`) in a `WITH` clause, especially inside a `CALL` subquery, Neo4j 5.x requires they be explicitly aliased (e.g., `WITH $repoKey AS repoKey`). Failing to do so results in a `42N21: Expression in WITH must be aliased (use AS)` error.
- **Case-Insensitive Repository Keys**: Repository keys (`repoKey`) are normalized to lowercase at the source (e.g., in the `Options` class) to ensure case-insensitivity for the repository identity. However, derived keys (e.g., `FileKey`, `SymbolKey`) should only have the `repoKey` prefix normalized; the remaining parts of the key (file paths, namespaces, symbol names) MUST remain case-sensitive to accurately reflect the source code and avoid unintended collisions or display issues.
- **Class Member Order**: Maintain a strict order for class members from top to bottom: 1. Constructors, 2. Public members, 3. Internal members, 4. Protected members, 5. Private members, 6. Private static members, 7. Private const members. (CRITICAL: Private constants must always be at the very bottom of the class).
- **FakeItEasy and IFileSystem.Path.Combine**: When using `FakeItEasy` to mock `IFileSystem.Path.Combine`, prefer two-argument overloads (e.g., `Path.Combine(a, b)`). Expression trees used by `A.CallTo` have difficulty with the expanded form of `params` array collection parameters, making multi-argument `Path.Combine(a, b, c, d, e)` calls significantly harder to mock correctly in unit tests.
- **Conditional Requirements**: If some options are required only in certain modes, remove `IsRequired()` from the option definition and enforce the requirement manually in a `RootCommand` validator.

## Validation Logic Management
- As command-line validation logic grows (e.g., checking for mutual exclusivity, conditional requirements, or complex option combinations), it can clutter `Program.cs` or `OptionsBinder.cs`.
- Extract this logic into a dedicated `OptionsBinderValidator` class (e.g., in a `Validation` namespace) to improve testability and separate concerns.
- Use a static `Validate` method that accepts the `CommandResult` and all relevant `Option` instances. This allows testing the validation logic in isolation without needing a full `BinderBase` or `RootCommand` execution.
- When testing validators in isolation, ensure that any `Option<T>` with default values in the production code also has the same default values in the test setup. Otherwise, `GetValueForOption` might return unexpected values (like `default(T)` instead of the desired default), causing false-positive validation errors in tests.
- In `OptionsBinder`, simply call `command.AddValidator(result => OptionsBinderValidator.Validate(result, ...))`.
- **System.CommandLine Default Values and Validation**: When an `Option<T>` has a default value, `result.GetValueForOption(option)` will return that default value even if the user did not explicitly provide the option on the command line. If you need to validate whether the user *actually* provided the option (e.g., to enforce mutual exclusivity with another flag), use `result.FindResultFor(option)` and check that the returned `OptionResult` is not `null` and `!result.IsImplicit`. 
- **Chain of Responsibility for CLI Execution**: For complex command-line tool workflows, extracting the `SetHandler` logic into a Chain of Responsibility (CoR) pattern improves modularity and testability. Each step (e.g., confirmation, environment setup, processing) becomes a discrete handler that can be tested in isolation and easily reordered or extended.
- **Handler Context**: When using a CoR pattern for application startup, use a `HandlerContext` object to pass shared state (like an `IServiceProvider`) between handlers. This avoids polluting the `Options` class with runtime-only dependencies.
- **Member Order Vigilance**: When refactoring or adding new classes, always double-check the project's member order guidelines (e.g., constructors, then public, then internal, then protected, then private). It's easy to overlook this during complex refactorings.
## Roslyn and C# Syntax
- **Event Symbols**: In Roslyn, field-like events (e.g., `public event EventHandler MyEvent;`) are represented by `EventFieldDeclarationSyntax`, which can contain multiple variables. `semanticModel.GetDeclaredSymbol(member)` on the `EventFieldDeclarationSyntax` itself returns `null`. You must iterate over `efds.Declaration.Variables` and call `GetDeclaredSymbol` on each variable to get the `IEventSymbol`.
- **Event Declaration Syntax**: Events with accessors (e.g., `public event EventHandler MyEvent { add { } remove { } }`) are represented by `EventDeclarationSyntax`. `semanticModel.GetDeclaredSymbol(eds)` correctly returns the `IEventSymbol`.
- **Member Dependency Extraction**: When extracting dependencies (e.g., `DEPENDS_ON` relationships) for class members, ensure you handle all member types:
    - Methods/Constructors/Operators: Parameters and Return Type.
    - Properties: Property Type.
    - Events: Event/Delegate Type (including handling of `Nullable<T>` wrappers).
    - Fields: Field Type.
- **Explicit Interface Implementations**: Explicit interface implementations (e.g., `void IInterface.Method()`) are treated as `Accessibility.Private` (or sometimes `Accessibility.NotApplicable`) by Roslyn. If filtering by accessibility, these must be explicitly detected using `ExplicitInterfaceImplementations.Any()` on the symbol to ensure they are ingested.
- **IErrorTypeSymbol and Test Contexts**: Be careful when filtering out `IErrorTypeSymbol`. In unit tests with limited assembly references, many types (like `EventHandler`) may be resolved as `IErrorTypeSymbol`. If you strictly skip these, you might break tests that expect certain relationships to be created even when the type isn't fully resolved. Ensure the logic allows for processing symbols even if they are not perfectly resolved when appropriate.
- **Unit Testing Dependencies**: When writing unit tests for symbol extraction that involve types from other assemblies (like `EventHandler` from `System`), ensure the `AdhocWorkspace` project has the necessary metadata references (e.g., `MetadataReference.CreateFromFile(typeof(EventHandler).Assembly.Location)`). If a type is not found, Roslyn will treat it as an error type, which might cause dependency extraction to skip it.
## Option Binding and Simplification
- When multiple command-line switches represent different ways to set a single application-level setting (e.g., `--log-level`, `--debug`, `--verbose`, and `--quiet`), consolidate them into a single property within the `Options` class.
- Handle the logic for resolving these mutually exclusive options within the `BinderBase<Options>.GetBoundValue` method. This keeps the rest of the application simple and unaware of the specific CLI flags used.
- Ensure a custom validator is used on the `RootCommand` to prevent the user from passing more than one of these options at once, maintaining a clear and unambiguous configuration state.
- **Redundant Command Prefixes**: When wrapping an external tool's execution (e.g., calling `dotnet dotnet-suggest` from C#), ensure the arguments do not redundantly include the command name if it's already part of the `dotnet` host invocation. For example, use `RunCommand("dotnet", "dotnet-suggest script")` instead of `RunCommand("dotnet", "dotnet-suggest dotnet-suggest script")`.
- **Global Tool Execution and CoreCLR Errors**: On some environments (especially macOS or resource-constrained shells), running a .NET global tool directly (e.g., `dotnet-suggest`) may fail with `Failed to create CoreCLR, HRESULT: 0x80070008`. In these cases, try running the tool via `dotnet exec` by pointing to the actual `.dll` in the `.dotnet/tools/.store` directory. This is more robust than relying on the shim executable.
- **System.CommandLine Shell Types**: When calling `dotnet-suggest script`, the shell type argument is case-sensitive and must match the `ShellType` enum (e.g., `Zsh`, `Bash`, `PowerShell`). Using lowercase (e.g., `zsh`) will result in a parsing error.

## CI/CD and Test Integration
- For CI/CD environments like GitHub Actions, integrate test execution directly into the workflow file (`.github/workflows/build.yml`) rather than relying solely on MSBuild targets in `Directory.Build.props`. This provides better visibility of test steps and ensures that failures correctly stop the pipeline before deployment steps.
- Always include `pull_request` triggers in the workflow to ensure that all changes are validated by tests before they can be merged into the main branch.
- Use `dotnet test --no-build` in the CI pipeline to avoid redundant compilation after the initial `dotnet build` step.

## .NET SDK and Target Framework Consistency on Self-Hosted Runners
- When using self-hosted runners, the .NET SDK version specified in `global.json` should align with the installed SDKs on the runner.
- If `global.json` has `rollForward: disable`, the runner MUST have the exact SDK version (or at least the same major/minor if not using `latestFeature`).
- If a project (like a test project) targets a framework version (e.g., `net9.0`) that is not installed on the runner, `dotnet test` will fail to launch the test host, even if a newer SDK (e.g., `net10.0`) is present, unless roll-forward is allowed.
- **Centralizing Frameworks**: Consolidating `TargetFramework` into a root `Directory.Build.props` ensures all projects in the solution target the same version, reducing the risk of "framework not found" errors on specific runners.
- **Inheritance**: To ensure projects correctly "pick up" settings from `Directory.Build.props`, remove redundant properties (like `<TargetFramework>`, `<ImplicitUsings>`, or `<Nullable>`) from the individual `.csproj` files. This makes the props file the single source of truth.
- **Dotnet Global Tools on Self-Hosted Runners**: On self-hosted runners, the global dotnet tools directory (`$HOME/.dotnet/tools`) might not be in the `PATH` of the shell session executing the workflow steps. Always explicitly add this directory to the `GITHUB_PATH` after installing or updating a global tool (e.g., `echo "$HOME/.dotnet/tools" >> $GITHUB_PATH`).
- **Idempotent Tool Installation**: Use `dotnet tool update --global` instead of `dotnet tool install --global` in CI/CD workflows. This ensures the tool is updated to the latest version if already present, preventing errors that occur when attempting to install a tool that is already installed.

## Coding and Testing Standards
- **Class Member Order**: Maintain a strict order for class members from top to bottom: 1. Constructors, 2. Public members, 3. Internal members, 4. Protected members, 5. Private members, 6. Private static members, 7. Private const members. (CRITICAL: Private constants must always be at the very bottom of the class).
- **Unit Test Isolation**: Do not use constructors for global setup in unit tests to prevent state leakage and ensure isolation. Use `TestCaseSource` or local setup within each test.
- **Unit Test Naming**: Use the structured naming convention `Given[Scenario]_When[Action]_Then[Result]()` for all unit tests to clearly communicate intent and behavior.
- **Explicit interface implementations**: Explicit interface implementations (covered under "Roslyn and C# Syntax").

## GitHub Action Logic and Self-Hosted Runners
- **TRX File Accumulation**: On self-hosted runners, using `clean: false` in `actions/checkout` means that untracked directories like `./TestResults` from previous runs may persist. If `dotnet test` generates unique filenames for TRX files (the default behavior), these files will accumulate. Always clear the `TestResults` directory before running tests to ensure accurate metrics.
- **Comment Pagination**: When using `github.rest.issues.listComments` to find and update a bot comment, increase `per_page` (e.g., to 100) to ensure the target comment is found even in busy PRs. Using `reverse().find()` is a more robust way to find the most recent bot comment for updates.

## Path Normalization and Relative Path Ingestion
- When indexing code for a graph database like Neo4j, always use relative paths for file and symbol identifiers (`key` and `path`). Absolute paths (e.g., `/Users/chaseflorell/projects/...`) vary between different CI runners and local machines, leading to duplicated or inconsistent data.
- Compute relative paths using a stable base like the solution file's directory (`solutionRoot`).
- Ensure both the base path and the target file path are normalized (e.g., replacing backslashes with forward slashes) before computing the relative path.
- In `SolutionProcessor.cs`, explicitly compute and pass the `relativePath` to all handlers and mapper services to prevent accidental use of absolute paths.
- Renaming properties from `FilePath` to `RelativePath` in data records (like `Symbol` and `FileMetaData`) makes the intent clear and helps catch accidental usage of absolute paths through compilation errors in constructor calls using named parameters.

## Roslyn Extraction for Mapped Files (Razor/Xaml)
- **Mapped Line Spans**: When using Roslyn to analyze generated C# code (e.g., from `.razor` or `.xaml` files), use `GetMappedLineSpan()` instead of `GetLineSpan()` on `Location` objects. This allows mapping the generated C# symbols back to their original source locations in the `.razor` or `.xaml` files.
- **Syntax Tree Detection**: To find the generated C# code associated with a specific `.razor` or `.xaml` file, iterate through `compilation.SyntaxTrees` and check if any type declaration (`BaseTypeDeclarationSyntax`) in the tree has a `MappedLineSpan` pointing to the original file path.
- **Shared Processor Logic**: Extracting C# symbol processing logic into a shared service (e.g., `IRoslynSymbolProcessor`) allows it to be reused across different handlers (`CSharpHandler`, `RazorHandler`, `XamlHandler`). This ensures consistent extraction of members, accessibility, and dependencies regardless of the source file type.
- **Field and Event Field Declarations**: In Roslyn, `FieldDeclarationSyntax` and `EventFieldDeclarationSyntax` do not directly represent a single symbol; they can contain multiple variable declarations. Always iterate over `fds.Declaration.Variables` and call `GetDeclaredSymbol` on each variable to correctly extract all fields and events.

## XAML Namespace Handling
- XAML files can use various XML namespaces for the `x` prefix (e.g., `http://schemas.microsoft.com/winfx/2006/xaml`, `http://schemas.microsoft.com/winfx/2009/xaml`) and default presentation namespaces (MAUI, Xamarin.Forms, WPF, etc.).
- When extracting XAML attributes like `x:Class`, `x:Name`, or `x:Key`, check across all known XAML namespaces instead of hardcoding a single one.
- Attributes without a prefix (e.g., `Name="Foo"`) are in the empty namespace but should still be checked as they are often used as an alternative to `x:Name` in many XAML frameworks.
- **NuGet Dependency Ingestion**: When ingesting external dependencies (NuGet packages), use a version-less `key` (e.g., `pkg:PackageName`) for the database node. This identifies the "package" as a single entity. Store the specific `version` as a separate attribute on the node. This allows for better visualization and querying of package usage while satisfying the requirement for unique identification across different environments.
- **Symbol Versions**: For `PackageReference` symbols in `.csproj` files, add a `Version` property to the `Symbol` record and store it as a dedicated `version` attribute in Neo4j. This is more structured than including it in the `Documentation` or `Fqn` fields alone.
- **Data Purging and Dependencies**: When implementing a "purge data" feature, ensure that it also cleans up orphaned dependencies (e.g., NuGet packages) unless explicitly skipped via the `--skip-dependencies` flag. For shared resources like `Dependency` nodes, use a conditional purge that only deletes nodes if they are no longer referenced by any other `Project` in the database. Using `NOT EXISTS { MATCH (other:Project)-[:DEPENDS_ON]->(d) WHERE other.key <> $repoKey }` in Neo4j 5.x allows for safely identifying these orphans during a project-specific purge.
- **Neo4j Variable Scope and Parameters**: In Neo4j Cypher, parameters (e.g., `$repoKey`) are globally accessible in all scopes, including `CALL` subqueries and `EXISTS` blocks. However, if you alias a parameter to a variable (e.g., `WITH $repoKey AS repoKey`), that variable MUST be explicitly passed into subsequent scopes using `WITH`. Crucially, the `LIMIT` clause in Cypher DOES NOT accept variables; it only accepts literals or parameters. To avoid "Variable not defined" or "It is not allowed to refer to variables in LIMIT" errors, prefer using parameters (`$param`) directly in `WHERE`, `EXISTS`, and `LIMIT` clauses instead of aliasing them to local variables.
- **Async Await vs. ContinueWith in Option Handlers**: When implementing a chain of handlers for CLI options, always `await` asynchronous calls in `HandleOptions`. Avoid using `.ContinueWith(_ => true)` as it can swallow exceptions and make the application exit silently without logging errors, even if the main entry point has a try-catch block.
- **Chain of Responsibility in Option Handlers**: When implementing a chain of handlers for CLI options, ensure that handlers intended to be additive (like `PurgeExecutionHandler` and `SolutionProcessingHandler`) return `true` to allow the chain to continue. Returning `false` terminates the processing, which is only desired for exclusive operations or failed validations.
- **Git Ingestion Order**: Ingesting Git history (`UpsertCommits`) after processing files (`FlushFiles`) allows the commit-to-file linking logic to find existing `:File` nodes by their `path`. This is especially important when `:File` nodes use non-path keys (like FQN for Roslyn files), as it prevents the creation of duplicate `:File` nodes for the same physical file.
- **Unique Constraints and Git History**: When ingesting Git history, avoid using non-unique properties (like just the filename) as the `key` for `:File` nodes. This can cause `ConstraintViolation` errors in Neo4j when multiple files in the repository have the same name. Always use a unique identifier like the relative path as a fallback `key` during history ingestion.
- **Neo4j Database Naming**: Neo4j 5.0+ generally requires lowercase database names. Using uppercase letters in the database name can lead to connection errors or unexpected behavior where the driver falls back to the default database if not properly configured. Warn the user if an uppercase database name is provided.
- **Batched Deletions for Large Data Sets**: When purging large amounts of data in Neo4j, use batched deletions to avoid exceeding transaction memory limits. Implement a loop in C# that repeatedly executes a `LIMIT`ed `DETACH DELETE` query. Ensure the `LIMIT` uses a parameter (e.g., `LIMIT $batchSize`) rather than a variable. For project-specific purges, use `UNION ALL` to prioritize deleting leaf nodes (Symbols) before their parents (Files, Projects) to maintain the integrity of `EXISTS` filters across multiple batches.

## Roslyn File Key and Node Structure
- For Roslyn-based files (.cs, .razor, .xaml), use the fully qualified class name (FQN) as the `FileKey` instead of the file path.
- In Neo4j, merge `:File` nodes based on this `FileKey`. This naturally handles partial classes by merging multiple physical files into a single logical `File` node in the graph.
- When merging on a unique `key`, ensure that `path` is updated as a property. If multiple files merge, the `path` property will reflect the last file processed in the batch.
- To avoid unique constraint violations in Neo4j during batched updates, always use the unique identifier (the `key`) as the merge key in Cypher (`MERGE (f:File {key: file.fileKey})`).
- When deleting prior symbols for a file, use the `fileKey` to ensure all symbols associated with that logical file (including all parts of a partial class) are cleared before re-ingestion.
- For non-Roslyn files, use the relative path as the `FileKey` to maintain a one-to-one mapping between physical files and `:File` nodes.
- Always include a `fileName` property (name + extension) on `:File` nodes for easier querying and display, separate from the `key` or `path`.


## Thread-Safe and Context-Aware Logging
- When including thread-specific metadata in logs (e.g., `[MAIN-1]`, `[TASK-N]`), use `Environment.CurrentManagedThreadId` for reliable thread identification.
- Capture the "Main" thread ID during application startup (e.g., via a static field in the logger class initialized on first use, or explicitly set during dependency registration) to distinguish it from background tasks or pool threads.
- Differentiate between thread types using properties like `Thread.CurrentThread.IsThreadPoolThread`. This helps provide clearer diagnostic information by categorizing threads (e.g., MAIN vs TASK vs FINALIZER).