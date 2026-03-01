# Architecture
- `ISolutionProcessor` is the entry point for solution processing. It coordinates file discovery, git diffing, dependency ingestion, and file-by-file symbol extraction.
- `ISolutionFileDiscoveryService` is responsible for finding files to process. It considers regular documents, additional documents, and files from disk in the solution directory.
- `IDocumentHandler` is the interface for processing individual files. It uses `TextDocument` to allow for broad file type support (including `AdditionalDocument`).
- `CSharpHandler`, `RazorHandler`, `HtmlHandler`, `JavaScriptHandler`, `XamlHandler`, `XmlHandler`, `JsonHandler`, `CssHandler`, and `CsprojHandler` implement specific file processing logic.
- `IGraphService` handles interaction with the Neo4j database, including upserting symbols and relationships.
