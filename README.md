# CodeToNeo4j

[![Verify Build and Test](https://github.com/ChaseFlorell/CodeToNeo4j/actions/workflows/verify.yml/badge.svg)](https://github.com/ChaseFlorell/CodeToNeo4j/actions/workflows/verify.yml)
[![NuGet](https://img.shields.io/nuget/v/CodeToNeo4j.svg)](https://www.nuget.org/packages/CodeToNeo4j)
[![codecov](https://codecov.io/gh/ChaseFlorell/CodeToNeo4j/branch/main/graph/badge.svg)](https://codecov.io/gh/ChaseFlorell/CodeToNeo4j)

CodeToNeo4j is a .NET 10 console application designed to analyze .NET solutions and index their codebase structure (projects, files, symbols, and relationships) into a Neo4j knowledge base. It leverages Roslyn for semantic code analysis and Neo4j for powerful graph-based querying of your architecture.

## Setup

### Prerequisites
- **.NET 10 SDK**: Specifically version `10.0.201` (defined in `global.json`).
- **Neo4j Database**: Version 5.0 or higher.
- **Git**: Required if using incremental indexing (`--diff-base`) or tracking file authors.
- **Dart SDK** *(optional)*: Required only if analyzing Dart projects. Install from [dart.dev/get-dart](https://dart.dev/get-dart). The `dart` executable must be on your `PATH`. If the Dart SDK is not found, `.dart` files are skipped with a warning.

### Installation

#### As a .NET Global Tool

The recommended way to use CodeToNeo4j is as a .NET Global Tool. This allows you to run the `codetoneo4j` command from any directory.

1. **Install the tool**:
   ```bash
   dotnet tool install --global CodeToNeo4j
   ```
2. **Run the tool**:
   ```bash
   codetoneo4j --input ./MySolution.sln --password your-pass --database my-db --uri bolt://localhost:7687
   ```

To update the tool to the latest version:
```bash
dotnet tool update --global CodeToNeo4j
```

### Versioning

The tool follows a `1.0.[GITHUB_RUN_NUMBER]` versioning scheme for automated builds. You can find the specific version in the GitHub Actions run details or by checking the NuGet package properties.

#### Local Development

If you are developing CodeToNeo4j and want to test changes locally as a global tool, you can use the provided `run.sh` script:

1. **Run the script**:
   ```bash
   ./run.sh
   ```
   This script cleans the `artifacts/` directory, packs the tool with a local development version (default `0.0.1`), uninstalls any existing global `codetoneo4j`, and installs the new build globally from `./artifacts`.

#### From Source

1. Clone the repository:
   ```bash
   git clone https://github.com/chaseflorell/CodeToNeo4j.git
   cd CodeToNeo4j
   ```
2. Build the project:
   ```bash
   dotnet build -c Release
   ```
3. The executable will be located at `src/CodeToNeo4j/bin/Release/net10.0/CodeToNeo4j`.

## Usage

You can run the tool by pointing it to a .NET solution file (`.sln`, `.slnx`), a project file (`.csproj`), or a directory. When `--input` is omitted, the tool auto-detects the project type from the current directory.

If installed as a global tool:
```bash
codetoneo4j --password your-neo4j-password --database my-custom-db
```

Or with an explicit input:
```bash
codetoneo4j --input /path/to/YourSolution.sln --password your-neo4j-password
codetoneo4j --input /path/to/Project.csproj --password your-neo4j-password
codetoneo4j --input /path/to/project-dir --password your-neo4j-password
```

### Options

| Option                        | Description                                                                                                                | Default |
|-------------------------------|----------------------------------------------------------------------------------------------------------------------------| --- |
| `--input`, `--sln`, `-s`      | Path to a `.sln`, `.slnx`, or `.csproj` file, or a directory. Auto-detects when omitted. The filename or directory name (without extension) is used as the **case-insensitive** repository key. | (auto-detect) |
| `--no-key`                    | Do not use a repository key. Use this if the Neo4j instance is dedicated to this repository.                              | `false` |
| `--password`, `-p`            | **Required**. Password for the Neo4j database.                                                                             | |
| `--uri`, `-u`, `--url`        | **Required**. The Neo4j connection string.                                                                                               | `bolt://localhost:7687` |
| `--user`                      | Neo4j username.                                                                                                            | `neo4j` |
| `--database`, `-db`           | Neo4j database name.                                                                                                       | `neo4j` |
| `--log-level`, `-l`           | The minimum log level to display.                                                                                          | `Information` |
| `--debug`, `-d`               | Turn on debug logging.                                                                                                     | `false` |
| `--verbose`, `-v`             | Turn on trace logging.                                                                                                     | `false` |
| `--quiet`, `-q`               | Mute all logging output.                                                                                                   | `false` |
| `--diff-base`                 | Optional git base ref (e.g., `origin/main`) for incremental indexing. Only changed files since this ref will be processed. | |
| `--batch-size`                | Number of symbols to batch before flushing to Neo4j.                                                                       | `500` |
| `--skip-dependencies`         | Skip NuGet dependency ingestion.                                                                                           | `false` |
| `--min-accessibility`         | The minimum accessibility level to index (e.g., `Public`, `Internal`, `Private`).                                          | `NotApplicable` |
| `--include`, `-i`             | File extensions to include. Can be specified multiple times.                                                               | `.cs`, `.razor`, `.xaml`, `.js`, `.ts`, `.tsx`, `.html`, `.xml`, `.json`, `.css`, `.csproj`, `.dart` |
| `--purge-data`                | Purge data from Neo4j associated with the repository key (case-insensitive).                                              | `false` |

> **Note**: When `--input` is omitted, the tool auto-detects the project type from the current directory in priority order: `.sln` > `.slnx` > `.csproj` > `pubspec.yaml` > files-only mode. If multiple files of the same type exist, the tool exits with an error asking you to specify `--input` explicitly. When using `--purge-data`, the tool will ask for confirmation before deleting any data. The repository key derived from the input filename or directory name is **case-insensitive** (normalized to lowercase). If `--include` is also specified, only the data for those file extensions will be purged. `--skip-dependencies` and `--min-accessibility` are not permitted with this switch. Only one of `--log-level`, `--debug`, `--verbose`, or `--quiet` can be used.

### Purge data

Purge data previously ingested by this tool.

- Purge by derived repository key:
  ```bash
  codetoneo4j --input ./MySolution.sln --password your-pass --purge-data
  ```
- Purge all CodeToNeo4j data (when using --no-key):
  ```bash
  codetoneo4j --no-key --password your-pass --purge-data
  ```
- Filtered purge (only specific file extensions):
  ```bash
  codetoneo4j --input ./MySolution.sln --password your-pass --purge-data --include .cs --include .razor
  ```

You will be prompted to confirm before deletion proceeds.

## GitHub Actions Integration

To use CodeToNeo4j as part of your CI/CD pipeline, you can install it as a .NET Global Tool.

```yaml
jobs:
  index-code:
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Required for git diff

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Install CodeToNeo4j
        run: dotnet tool install --global CodeToNeo4j

      - name: Run CodeToNeo4j
        run: |
          codetoneo4j \
            --input ./MySolution.sln \
            --uri ${{ secrets.NEO4J_URL }} \
            --password ${{ secrets.NEO4J_PASS }} \
            --database my-database \
            --log-level Information \
            --diff-base ${{ github.event.before }}
```

## Limitations

- **.NET SDK Dependency**: The machine running the tool must have the .NET SDK installed (specifically the version matching the solution being analyzed) because `MSBuildLocator` needs to find a valid MSBuild instance to load the solution.
- **Neo4j Version**: Only Neo4j 5.x and above are supported due to the use of `IF NOT EXISTS` syntax in Cypher schema commands.
- **Supported File Types**: Analyzes `.cs`, `.razor`, `.xaml`, `.js`, `.ts`, `.tsx`, `.html`, `.xml`, `.json`, `.css`, `.csproj`, and `.dart` files (configurable via `--include`).
    - **C#**: Full symbol extraction (Classes, Methods, etc.) and semantic mapping.
    - **Razor**: Extracts directives such as `@using`, `@inject`, `@model`, and `@inherits`.
    - **XAML**: Extracts UI elements and event handler bindings (e.g., `Click`, `Command`).
    - **JavaScript**: Extracts function definitions and import statements.
    - **TypeScript** (`.ts`, `.tsx`): Extracts function definitions, import statements, interfaces, type aliases, and enums.
    - **HTML**: Extracts script references and element IDs.
    - **XML**: Extracts hierarchical element structure.
    - **JSON**: Extracts properties as symbols.
    - **CSS**: Extracts CSS selectors.
    - **Csproj**: Extracts `PackageReference`, `ProjectReference`, and project properties (e.g., `OutputType`, `TargetFramework`).
    - **Dart**: Full semantic analysis via the Dart `analyzer` package. Extracts classes, mixins, enums, extensions, functions, methods, constructors, fields, properties, operators, and type aliases. Captures `CONTAINS`, `DEPENDS_ON`, and `INVOKES` relationships. Requires the Dart SDK on `PATH`.
    - **pubspec.yaml**: Extracts project dependencies and dev dependencies as `DEPENDS_ON` relationships.
- **Symbol Depth**: Indexes Types (Classes, Enums, etc.) and their immediate members.
- **Documentation & Comments**: Ingests triple-slash XML documentation and standard code comments (`//`, `/* */`) for each symbol, enabling semantic search and context for LLMs.
- **Git Context**: Tracks file metadata including creation date, last modified date, commit hashes, git tags, and individual author statistics (name, email, first contribution, last contribution, and commit count) for each indexed file. When using incremental indexing (`--diff-base`), it also ingests detailed information for all commits in the specified range (including hashes, authors, dates, and commit messages), linking them to the modified files. Commit ingestion is parallelized and uses `--batch-size` for efficient fetching and database updates. Deleted files are marked as `deleted: true` to preserve historical context. Incremental indexing and commit history tracking require a valid Git repository and the `git` executable in the PATH.

---

## Gap Analysis Report

This analysis identifies potential "gotchas" and missing features for the enterprise-ready "run from anywhere" and "CI integration" goal.

### 1. Authentication & Security
**Gap**: Only Basic Authentication is currently supported.
**Gotcha**: Production Neo4j instances might use LDAP, SSO, or Kerberos. Passing passwords via CLI arguments can also leak them in process lists.
**Recommendation**: Support environment variables for sensitive parameters like `--pass`. Support for environment variables for all options is planned.

### 2. Database Schema Migrations
**Gap**: The tool runs `EnsureSchemaAsync` on every run.
**Gotcha**: While it uses `IF NOT EXISTS`, any *changes* to the schema (e.g., adding a new index) require manual update of `Schema.cypher`. It doesn't handle destructive migrations or schema versioning.
**Recommendation**: Implement a simple schema versioning mechanism if the graph model evolves.

### 3. Performance with Large Repos
**Gap**: Batching is implemented, but the entire solution is loaded into memory via Roslyn.
**Gotcha**: Very large solutions (thousands of projects) might exceed memory limits in standard CI runners (e.g., 7GB on GitHub hosted runners).
**Recommendation**: Consider processing projects one-by-one or in smaller groups if memory becomes an issue.

