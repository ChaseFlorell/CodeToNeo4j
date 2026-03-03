# Architecture
- `ISolutionProcessor` is the entry point for solution processing. It coordinates file discovery, git diffing, dependency ingestion, and file-by-file symbol extraction.
- `ISolutionFileDiscoveryService` is responsible for finding files to process. It considers regular documents, additional documents, and files from disk in the solution directory.
- `IDocumentHandler` is the interface for processing individual files. It uses `TextDocument` to allow for broad file type support (including `AdditionalDocument`).
- `CSharpHandler`, `RazorHandler`, `HtmlHandler`, `JavaScriptHandler`, `XamlHandler`, `XmlHandler`, `JsonHandler`, `CssHandler`, and `CsprojHandler` implement specific file processing logic.
    - `CsprojHandler` extracts `PackageReference`, `ProjectReference`, and `PropertyGroup` properties (as `ProjectProperty` symbols).
- `IGraphService` handles interaction with the Neo4j database, including upserting symbols and relationships.

# Performance Principles
- **Batching**: Always prefer batching I/O operations (database writes, git commands) to minimize overhead.
- **Concurrency**: Use `Parallel.ForEachAsync` for independent CPU-bound tasks, but cap the degree of parallelism (default: 20) to manage memory and resource pressure.
- **Lazy Loading**: Avoid pre-loading heavy objects (like Roslyn compilations) for the entire solution. Load them only when needed for the current batch of work.
- **Producer-Consumer**: Decouple analysis from ingestion using `System.Threading.Channels` to ensure a responsive UI and optimal database batching.
- **Async Efficiency**: Use `Task` and `ConfigureAwait(false)` for all I/O-bound code. Reserve `ValueTask` for proven, high-frequency synchronous hot paths.
