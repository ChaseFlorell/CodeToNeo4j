# Architecture
- `ISolutionProcessor` is the entry point for solution processing. It coordinates file discovery, git diffing, dependency ingestion, and file-by-file symbol extraction.
- `ISolutionFileDiscoveryService` is responsible for finding files to process. It considers regular documents, additional documents, and files from disk in the solution directory.
- `IDocumentHandler` is the interface for processing individual files. It uses `TextDocument` to allow for broad file type support (including `AdditionalDocument`).
- `CSharpHandler`, `RazorHandler`, `HtmlHandler`, `JavaScriptHandler`, `XamlHandler`, `XmlHandler`, `JsonHandler`, `CssHandler`, and `CsprojHandler` implement specific file processing logic.
- `IGraphService` handles interaction with the Neo4j database, including upserting symbols and relationships.
- **Data Consistency**: Repository keys (`repoKey`) are normalized to lowercase in the application before being sent to Neo4j. This ensures case-insensitivity for the repository identity. Derived keys (e.g., `FileKey`, `SymbolKey`) use the normalized `repoKey` as a prefix, but the remaining parts (file paths, namespaces, symbol names) remain case-sensitive to match the source code.
- **Neo4j 5.x Requirements**:
    - Use `IF NOT EXISTS` for all schema commands.
    - Explicitly alias all parameters in `WITH` clauses (e.g., `WITH $param AS param`), especially within `CALL` subqueries.
- **Tab Completions**:
    - Use `dotnet-suggest` for tab completions.
    - Implement a `IConsoleCompletionsService` to handle terminal detection and `dotnet-suggest` configuration.
    - Supported shells: zsh, bash, pwsh.
- **Class Member Order**: Maintain a consistent order for class members from top to bottom:
    1. Constructors
    2. Public members
    3. Internal members
    4. Protected members
    5. Private members
    6. Private static members
    7. Private const members (CRITICAL: These must be at the very bottom of the class)

# Performance Principles
- **Batching**: Always prefer batching I/O operations (database writes, git commands) to minimize overhead.
- **Concurrency**: Use `Parallel.ForEachAsync` for independent CPU-bound tasks, but cap the degree of parallelism (default: 20) to manage memory and resource pressure.
- **Lazy Loading**: Avoid pre-loading heavy objects (like Roslyn compilations) for the entire solution. Load them only when needed for the current batch of work.
- **Producer-Consumer**: Decouple analysis from ingestion using `System.Threading.Channels` to ensure a responsive UI and optimal database batching.
- **Async Efficiency**: Use `Task` and `ConfigureAwait(false)` for all I/O-bound code. Reserve `ValueTask` for proven, high-frequency synchronous hot paths.

# Coding Standards
- **Class Member Order**: Maintain a consistent order for class members from top to bottom:
    1. Constructors
    2. Public members
    3. Internal members
    4. Protected members
    5. Private members
    6. Private static members
    7. Private const members

# Testing Standards
- **Unit Test Naming**: All unit tests must follow the pattern: `Given[Scenario]_When[Action]_Then[Result]()`.
- **Test Setup**: Avoid global setup in constructors. Use `TestCaseSource` or scoped variables within the test method to ensure test isolation and clarity.

# Documentation Standards
- **README Synchronicity**: Every time `README.md` is updated, `PACKAGE_README.md` MUST also be updated to ensure they remain in sync. 
- **README Content**: `README.md` and `PACKAGE_README.md` might contain slightly different information due to their different uses, but the overall commandline switches and instructions should match.
