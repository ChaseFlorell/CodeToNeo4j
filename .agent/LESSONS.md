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

## SRP in SolutionProcessor

- Large orchestration classes like `SolutionProcessor` tend to accumulate responsibilities (File discovery, Git diffing, Dependency extraction, DB initialization).
- Extracting these into services (e.g., `ISolutionFileDiscoveryService`, `IDependencyIngestor`) makes the main orchestration logic much clearer.
- Using `TextDocument` instead of `Document` allows for better interoperability between regular documents and additional documents in Roslyn.

## Roslyn Interoperability and Extraction

- When handling different file types in a Roslyn solution, prefer `TextDocument` over `Document` in interfaces that need to handle `AdditionalDocument`, `AnalyzerConfigDocument`, or just regular files from disk.
- If syntax tree operations are required (e.g., in `CSharpHandler`), cast the `TextDocument` to `Document` locally.
- **Event Symbols**: Field-like events (`EventFieldDeclarationSyntax`) can contain multiple variables. Call `GetDeclaredSymbol` on each variable, not the declaration itself.
- **Event Declaration Syntax**: Events with accessors (`EventDeclarationSyntax`) work directly with `GetDeclaredSymbol`.
- **Member Dependency Extraction**: Handle all member types: Methods/Constructors/Operators (Parameters, Return Type), Properties (Property Type), Events (Event/Delegate Type including `Nullable<T>`), Fields (Field Type).
- **Explicit Interface Implementations**: Treated as `Accessibility.Private` or `NotApplicable` by Roslyn. Detect using `ExplicitInterfaceImplementations.Any()` to ensure they are ingested.
- **IErrorTypeSymbol**: In unit tests with limited assembly references, many types resolve as `IErrorTypeSymbol`. Ensure logic allows processing even when types aren't fully resolved.
- **Mapped Line Spans**: For generated C# (from `.razor`/`.xaml`), use `GetMappedLineSpan()` instead of `GetLineSpan()` to map back to original source locations.
- **Syntax Tree Detection**: Find generated C# for a `.razor`/`.xaml` file by checking `MappedLineSpan` in `compilation.SyntaxTrees`.
- **Shared Processor Logic**: Extract C# symbol processing into a shared service (`IRoslynSymbolProcessor`) for reuse across handlers.
- **Field and Event Field Declarations**: Always iterate over `fds.Declaration.Variables` for correct extraction.

## Async Performance and Task Selection

- Use `Task` and `Task<T>` as the default for most async methods, especially I/O-bound operations.
- Reserve `ValueTask` for high-performance hot paths where operations frequently complete synchronously.
- Always use `ConfigureAwait(false)` for library/non-UI code.
- A `ValueTask` should only be awaited once; convert to `Task` via `.AsTask()` if needed multiple times.
- Ensure `Main` returns `Task<int>` or `int` — `ValueTask<int>` is not a valid entry point in all environments.

## Console Output and Progress

- Use `Console.Write("\r...")` for in-place single-line updates.
- Branch progress behavior based on `LogLevel`: `Information` = single-line, `Debug` or lower = multi-line, `Warning` or higher = suppress.
- A custom `ILogger` can manage output formatting and suppress specific logs during progress reporting.
- For fatal errors, use `Console.Error.WriteLine(ex.ToString())` before exiting with `HResult`.

## .NET Global Tool Packaging

- Multi-targeting (`net10.0;net9.0;net8.0`) improves compatibility with older `dotnet` CLI versions during tool installation.
- Ensures `DotnetToolSettings.xml` is present in expected framework-specific folders.

## Performance and Architecture Patterns

- **Producer-Consumer**: Decouple analysis from ingestion using `System.Threading.Channels`.
- **Batched Git Metadata**: Pre-fetch metadata for all files in a single `git log --name-only` pass.
- **Lazy Roslyn Loading**: Store only `ProjectId`/`DocumentId`; load Roslyn objects lazily within the parallel loop.
- **Database Batching with UNWIND**: Batch multiple operations into a single transaction.
- **Streaming I/O**: Use streaming APIs for file hashing to prevent large allocations.
- **Lock-Free Batching**: A dedicated consumer task eliminates complex locking in the parallel loop.

## Command-Line Design and Data Purging

- Always implement a confirmation prompt for destructive admin commands.
- Use `AddValidator` on `RootCommand` or `Option`s for argument validation instead of throwing in `SetHandler`.
- Make mandatory options optional in the binder when not relevant to the specific mode; enforce via validators.
- **Case-Insensitive Repository Keys**: `repoKey` is normalized to lowercase; derived keys keep remaining parts case-sensitive.
- **FakeItEasy and IFileSystem.Path.Combine**: Prefer two-argument overloads; `params` array expressions are hard to mock.
- **Conditional Requirements**: Remove `IsRequired()` from option definitions when requirements are mode-dependent; enforce manually in validators.

## Validation Logic Management

- Extract validation logic into a dedicated `OptionsBinderValidator` class for testability.
- Use `result.FindResultFor(option)` and `!result.IsImplicit` to check if the user actually provided an option.
- **Chain of Responsibility**: Extract `SetHandler` logic into a CoR pattern; use a `HandlerContext` to pass shared state.

## Option Binding and Simplification

- Consolidate mutually exclusive switches (e.g., `--log-level`, `--debug`, `--verbose`, `--quiet`) into a single `Options` property.
- Handle resolution in `BinderBase<Options>.GetBoundValue`.
- **Redundant Command Prefixes**: When wrapping external tools, don't redundantly include the command name.
- **System.CommandLine Shell Types**: `dotnet-suggest script` shell type argument is case-sensitive (e.g., `Zsh`, not `zsh`).

## CI/CD and Test Integration

- Integrate test execution directly into the workflow file for better visibility.
- Always include `pull_request` triggers.
- Use `dotnet test --no-build` to avoid redundant compilation.

## Path Normalization and Relative Path Ingestion

- Always use relative paths for file and symbol identifiers in Neo4j.
- Compute relative paths using the solution file's directory (`solutionRoot`).
- Normalize both base and target paths (replace backslashes with forward slashes) before computing relative paths.

## XAML Namespace Handling

- Check across all known XAML namespaces for attributes like `x:Class`, `x:Name`, `x:Key`.
- Attributes without a prefix are in the empty namespace but should still be checked.

## NuGet Dependency Ingestion

- Use a version-less `key` (e.g., `pkg:PackageName`) for the database node; store specific `version` as a separate attribute.
- For `PackageReference` symbols, add a `Version` property to the `Symbol` record.
- When purging, conditionally delete orphaned dependencies unless `--skip-dependencies` is set.

## Neo4j Graph Design for AI Consumers

- This graph is queried **exclusively by AI**, not by humans navigating a visual graph explorer.
- Always favor graph designs that are easier for AI to query correctly:
  - **Prefer flat properties over separate nodes** when the information is a simple label or scalar — simpler Cypher reduces AI query errors.
  - **Prefer token-efficient result shapes** — flat rows are cheaper for an AI to consume than multi-hop join results.
  - **Do not create node types or relationships solely for graph purity** — only add them if an AI genuinely needs to traverse them at query time.
  - **Technology metadata** (docs URLs, version strings, ecosystem links) has no practical value — the AI already knows what `.NET` or `Node.js` is from training.
  - **Inter-entity relationships** (e.g. `Technology INCLUDES Language`) add complexity the AI doesn't need — it understands those associations from training knowledge.

## Neo4j Best Practices

- All nodes must include `CodeToNeo4j: true` for purge identification.
- `Author` nodes are merged from multiple sources; ensure `SET a.CodeToNeo4j = true` in all `MERGE (a:Author ...)` blocks.
- Use batched deletions with `LIMIT $batchSize` for large purges.
- Use `UNION ALL` to prioritize deleting leaf nodes (Symbols) before parents (Files, Projects).
- **Async Await vs ContinueWith**: Always `await` async calls in handlers; avoid `.ContinueWith(_ => true)` which swallows exceptions.
- **Chain of Responsibility in Handlers**: Additive handlers return `true` to continue; exclusive modes (e.g., `--purge-data`) return `false` to stop.
- **Selective Purging**: Use `$purgeDependencies` boolean to conditionally skip `Dependency` node deletion.
- **Git Ingestion Order**: Ingest commits after processing files so commit-to-file linking finds existing `:File` nodes.
- **Unique Constraints**: Use unique identifiers (relative path) as `key` for `:File` nodes, not just filenames.
- **Database Naming**: Neo4j 5.0+ generally requires lowercase database names.

## Roslyn File Key and Node Structure

- Use relative path as `FileKey` for all files to maintain one-to-one mapping with physical files.
- Partial classes are handled by multiple `:File` nodes with `DECLARES` relationships to the same `:Symbol` node (keyed by FQN).
- Maintain stable symbol keys based on FQN (`[repoKey]:[fqn]`).

## Thread-Safe and Context-Aware Logging

- Use `Environment.CurrentManagedThreadId` for reliable thread identification.
- Capture the "Main" thread ID during startup to distinguish from background tasks.
- Differentiate thread types using `Thread.CurrentThread.IsThreadPoolThread`.

## GitHub Tooling

- **GitHub CLI (gh)**: Available and authenticated for repository management, PR creation, and workflow checks.
- **Solution file (.slnx) tracking**: Any time a new file is added under `.github/` (scripts, workflows, markdown, etc.), it must also be linked in `CodeToNeo4j.slnx` under the appropriate folder group.
- **`actions/github-script@v8` limitation**: The `script:` input is required and there is no `script-path` support. Inline `require()` wrappers (2 lines) are the minimum necessary when delegating to external `.js` files. If zero inline content is required, rewrite the step to use `gh` CLI or bash
  instead.

## Pull Request Requirements

- All PRs must follow the template in `.github/pull_request_template.md`.
- The `pr-requirements.yml` workflow enforces labels and `^Resolves #[0-9]+` in the body.
- Always apply the same label(s) to a PR as the issue it resolves.

## Dart Analyzer Bridge and CI

- **analyzer 12.x API changes**: `ClassDeclaration.name` and `EnumDeclaration.name` were removed; use `namePart.typeName.lexeme` instead. `ExtensionTypeDeclaration.name` was removed; use `primaryConstructor.typeName.lexeme`. `NamedType.name2` was removed; use `NamedType.name` (now a `Token`
  directly).
- **Dart installed via Flutter**: On macOS with a self-hosted runner, Dart ships inside the Flutter SDK cask. Use `subosito/flutter-action@v2` (not a standalone Dart action) to install the correct version on any agent.
- **Unresolved AST parsing and instance creation**: `parseString` performs heuristic (unresolved) parsing. `Foo()` without the `new` keyword is parsed as a `MethodInvocation`, not an `InstanceCreationExpression`. Use `new Foo()` in unit tests to exercise `visitInstanceCreationExpression`.
- **Dart coverage with lcov**: Generate coverage using `dart test --coverage=coverage`, then convert with `dart pub run coverage:format_coverage --lcov --in=coverage --out=coverage/lcov.info --report-on=lib`. Upload the lcov file to Codecov with a `flags: dart` tag alongside the .NET opencover
  reports.
- **Codecov patch vs project coverage**: Patch coverage only counts lines changed in the diff. Uncovered visitor methods (e.g., `visitExtensionTypeDeclaration`) only appear in patch coverage when their lines were modified. Use `codecov.yml` to set `patch.target` (e.g., 90%) and `project.threshold` (
  e.g., 1%) to prevent regressions.
- **Flutter SDK version pinning**: Pin `flutter-version` in `subosito/flutter-action` to guarantee a specific Dart SDK version, analogous to `global-json-file` for .NET. This ensures any fresh agent produces a reproducible build.

## Unit Testing and Coverage

- **Mocking Neo4j Driver**: Match the full signature including optional `Action<TransactionConfigBuilder>` parameter.
- **Extension Methods**: Mock the underlying interface methods, not the static extension methods.
- **IResultCursor Mocking**: Mock `FetchAsync()` to return `true` then `false`, and mock `Current` for the desired record.
- **Accessibility Filtering**: Check for explicit interface implementations using `ExplicitInterfaceImplementations.Any()`.
- **Unit Test Dependencies**: Ensure `AdhocWorkspace` projects have necessary metadata references for types from other assemblies.
