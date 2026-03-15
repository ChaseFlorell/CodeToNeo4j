# CodeToNeo4j Project Requirements

`CodeToNeo4j` is a CLI tool designed to index .NET solutions into a Neo4j graph database using Roslyn for static analysis and Git for metadata and history tracking.

## 1. Functional Requirements

### 1.1 Input
- **Solution File**: Path to a valid `.sln` or `.csproj` file.
- **Neo4j Configuration**: URI, Username, Password, and Database Name.
- **Repository Metadata**: The repository key is derived from the solution file name by default.
  - **No Key Mode**: (Optional) Use `--no-key` to ingest data without a `repoKey`, suitable for single-repository Neo4j instances. All data ingested by this tool is marked with metadata (`CodeToNeo4j: true`) for identification.

### 1.2 Solution and Project Analysis
- **Solution Loading**: Use `MSBuildWorkspace` to load and analyze .NET solutions and their constituent projects.
- **File Discovery**: Identify files to process based on a configurable list of extensions.
  - **Supported Extensions (Default)**: `.cs`, `.razor`, `.xaml`, `.js`, `.html`, `.xml`, `.json`, `.css`, `.csproj`.
- **Symbol Extraction**: Parse source code (via Roslyn) to extract:
  - Types (Classes, Interfaces, Structs, Records, Enums).
  - Members (Methods, Properties, Fields, Events).
  - Accessibility Filtering: Filter extracted symbols based on their declared accessibility (e.g., only Public, or Public and Protected). Default: `Private` (includes all).
  - Syntax mapping (Start/End line numbers, Fully Qualified Names).
  - Documentation and comments.
- **Relationship Extraction**: Identify relationships between symbols:
  - `CONTAINS`: Structural containment (e.g., Type contains Method, Class contains Nested Class).

### 1.3 Git Integration
- **File Metadata**: Retrieve author information (names, emails), commit counts, and timestamps (creation, last modification) for each file.
- **Commit History**: (Optional) Ingest commit history since a specified `diffBase`.
  - Link commits to the files they modified and the authors who made them.
  - Files modified by a commit that are not already indexed are automatically created and marked as `deleted: true`.
  - **Fidelity**: Capture all commits in the range, including hash, author, date, and message.
  - **Parallelized Ingestion**: Commits are fetched from git and ingested into Neo4j in parallel batches (controlled by `--batch-size`).
- **Incremental Indexing**: (Optional) Only process files that have changed since a specified `diffBase` (e.g., `origin/main`).
  - **Diff Range Support**: Support various git range specifications (e.g., `hash1..hash2`, `hash1...hash2`, `branch-name`).
- **Deletion Tracking**: Identify and mark files as `deleted: true` in Neo4j if they were removed from the repository.
- **Data Purging**:
  - Support deletion of data (Projects, Files, Symbols, Commits) associated with the derived repository key.
  - Support full deletion of all `CodeToNeo4j` ingested data when `--no-key` is used, by targeting metadata (`CodeToNeo4j: true`).
  - Support partial deletion by filtering on file extensions (via `--include`).
  - Require explicit user confirmation before any deletion operation.

### 1.4 Dependency Ingestion
- **NuGet Packages**: (Optional) Discover and record all NuGet dependencies used within the solution.
  - Link projects to the dependencies they use.

### 1.5 Neo4j Integration
- **Schema Management**: Automatically ensure required constraints and indexes are present in the target database.
- **Graph Model**:
  - **Nodes**: `Project`, `File`, `Symbol`, `Dependency`, `Author`, `Commit`.
  - **Relationships**:
    - `(Project)-[:HAS_FILE]->(File)`
    - `(File)-[:DECLARES]->(Symbol)`
    - `(Symbol)-[:CONTAINS]->(Symbol)`
    - `(Author)-[:AUTHORED {commitCount, firstCommit, lastCommit}]->(File)`
    - `(Author)-[:COMMITTED]->(Commit)`
    - `(Commit)-[:PART_OF_PROJECT]->(Project)`
    - `(Commit)-[:MODIFIED_FILE]->(File)`
    - `(Project)-[:DEPENDS_ON]->(Dependency)`
- **Transaction Safety**: Perform database writes within transactions with retry logic to ensure consistency and handle transient network/concurrency issues.

## 2. Non-Functional Requirements

### 2.1 Performance and Scalability
- **Parallel Processing**: Process multiple files concurrently using `Parallel.ForEachAsync` with a controlled degree of parallelism (default: 20).
- **Batching**: Batch symbol and relationship writes to Neo4j (default: 500) to minimize transaction overhead while maintaining granular file-level progress.
- **Async/Await**: Use asynchronous I/O and CPU-bound operations throughout the pipeline to maximize throughput.
- **Thread Safety**: Ensure all shared buffers and progress counters are thread-safe.

### 2.2 Observability and UI
- **Logging**: Support configurable log levels (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`).
- **Progress Reporting**:
  - Provide a single-line, "in-place" progress update in the terminal for `Information` level reporting.
  - Display progress as `[Progress: 99.62 %] [xxx/yyy] Processing <file_path>`.
  - Suppress detailed Neo4j flush logs during progress updates to keep the console clean.
- **Error Handling**: Gracefully handle and log exceptions, returning non-zero exit codes on failure (based on `HResult`).

### 2.3 Resource Management
- **Memory Efficiency**: Avoid loading all solution documents or semantic models into memory simultaneously to prevent `OutOfMemoryException`.
- **Database Connections**: Reuse the Neo4j driver instance throughout the application lifecycle and dispose of it correctly.

### 2.4 Compatibility
- **.NET Multi-targeting**: Support and target `net10.0`, `net9.0`, and `net8.0` for broad compatibility.
- **Neo4j Version**: Support Neo4j version 5.0 and higher.
- **OS Support**: Run on macOS, Windows, and Linux.

## 3. Extensibility
- **Document Handler Pattern**: Use an extensible handler architecture (`IDocumentHandler`) to allow for future support of additional file types and analysis rules.
- **Service-Oriented Architecture**: Decouple responsibilities (Git, Neo4j, File Discovery, Dependency Ingestion) into standalone services for easier testing and maintenance.

## 4. Environment and Tooling
- **Build System**: .NET SDK 8.0, 9.0, or 10.0.
- **MSBuild Support**: Uses `Microsoft.Build.Locator` to find and register a valid MSBuild instance on the host machine.
- **Global Tool**: Packable as a .NET global tool (`codetoneo4j`) for easy installation and global shell usage.
- **Development Workflow**: A `run.sh` script for local building, packing, and global installation testing.

## 5. Dependency Management (Renovate)

### 5.1 Summary
Set up [Renovate](https://docs.renovatebot.com/) to automatically manage dependency updates across the project.

### 5.2 Motivation
Keeping dependencies up to date manually is tedious and error-prone. Renovate automates this by opening PRs for dependency updates, allowing us to stay current with security patches and new releases with minimal effort.

### 5.3 Tasks
- [x] Add `renovate.json` configuration to the repository root
- [x] Configure Renovate for NuGet (.NET) package updates
- [x] Set appropriate auto-merge rules for patch/minor updates
- [x] Configure PR grouping and scheduling as needed
- [ ] Install/enable the Renovate GitHub App on the repository (User Action required)

### 5.4 Grouping Logic
- **Alignment with Directory.Packages.props**: Renovate groups are configured to match the `ItemGroup` labels within `Directory.Packages.props` exactly.
- **Microsoft and System**: All core `Microsoft.*` and `System.*` dependencies are grouped together under the "Microsoft and System" label to ensure they update in sync. This is particularly important for `Microsoft.Extensions.Logging` to stay aligned with other core packages.
