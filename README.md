# CodeToNeo4j

CodeToNeo4j is a .NET 10 console application designed to analyze C# solutions and index their codebase structure (projects, files, symbols, and relationships) into a Neo4j knowledge base. It leverages Roslyn for semantic code analysis and Neo4j for powerful graph-based querying of your architecture.

## Setup

### Prerequisites
- **.NET 10 SDK**: Specifically version `10.0.103` (defined in `global.json`).
- **Neo4j Database**: Version 5.0 or higher.
- **Git**: Required if using incremental indexing (`--diffBase`).

### Installation

#### As a .NET Global Tool

You can install CodeToNeo4j as a .NET Global Tool. This is the recommended way to use the tool across multiple projects.

1. Download the NuGet package (`.nupkg`) from the GitHub Actions artifacts.
2. Install the tool locally (or from a private NuGet feed):
   ```bash
   dotnet tool install --global --add-source ./path-to-nupkg CodeToNeo4j.Console
   ```
3. Run the tool using the `codetoneo4j` command:
   ```bash
   codetoneo4j --sln ./MySolution.sln --pass your-pass --repoKey my-repo --database my-db
   ```

### Versioning

The tool follows a `1.0.[GITHUB_RUN_NUMBER]` versioning scheme for automated builds. You can find the specific version in the GitHub Actions run details or by checking the NuGet package properties.

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
3. The executable will be located at `src/CodeToNeo4j.Console/bin/Release/net10.0/CodeToNeo4j.Console`.

## Usage

You can run the tool by pointing it to a C# solution file (`.sln`).

If installed as a global tool:
```bash
codetoneo4j --sln /path/to/YourSolution.sln --pass your-neo4j-password --repoKey my-project-name --database my-custom-db
```

If running from the build output:
```bash
./CodeToNeo4j.Console --sln /path/to/YourSolution.sln --pass your-neo4j-password --repoKey my-project-name
```

### Options

| Option | Description | Default |
| --- | --- | --- |
| `--sln` | **Required**. Path to the `.sln` file to index. | |
| `--repoKey` | **Required**. A unique identifier for the repository in Neo4j. | |
| `--pass` | **Required**. Password for the Neo4j database. | |
| `--neo4j` | The Neo4j connection string. | `bolt://localhost:7687` |
| `--user` | Neo4j username. | `neo4j` |
| `--database` | Neo4j database name. | `neo4j` |
| `--diffBase` | Optional git base ref (e.g., `origin/main`) for incremental indexing. Only changed files since this ref will be processed. | |
| `--batchSize` | Number of symbols to batch before flushing to Neo4j. | `500` |

## GitHub Actions Integration

To use CodeToNeo4j as part of your CI/CD pipeline to update your knowledge base on every push:

1. **Build and Upload CodeToNeo4j as an Artifact** (or use a pre-built release).
2. **Download and Run** in your target project's workflow:

```yaml
jobs:
  index-code:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Required for git diff

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      # Assuming you have the tool as an artifact or global tool
      - name: Run CodeToNeo4j
        run: |
          codetoneo4j \
            --sln ./MySolution.sln \
            --repoKey my-repo \
            --neo4j ${{ secrets.NEO4J_URL }} \
            --pass ${{ secrets.NEO4J_PASS }} \
            --database my-database \
            --diffBase ${{ github.event.before }}
```

## Limitations

- **.NET SDK Dependency**: The machine running the tool must have the .NET SDK installed (specifically the version matching the solution being analyzed) because `MSBuildLocator` needs to find a valid MSBuild instance to load the solution.
- **Neo4j Version**: Only Neo4j 5.x and above are supported due to the use of `IF NOT EXISTS` syntax in Cypher schema commands.
- **C# Only**: Currently only analyzes `.cs` files.
- **Symbol Depth**: Currently indexes Types (Classes, Enums, etc.) and their immediate members. Deep semantic analysis of method bodies (e.g., call graphs) is not yet fully implemented.
- **Git Context**: Incremental indexing requires a valid Git repository and the `git` executable in the PATH.

---

## Gap Analysis Report

This analysis identifies potential "gotchas" and missing features for the enterprise-ready "run from anywhere" and "CI integration" goal.

### 1. MSBuild Discovery in CI Environments
**Gap**: CodeToNeo4j explicitly discovers and registers the highest versioned MSBuild instance found on the machine.
**Gotcha**: In GitHub Actions or minimal Docker containers, if the required .NET SDK is not installed, the tool will fail to find an MSBuild instance.
**Recommendation**: Ensure the workflow environment has the correct .NET SDK installed that matches the solution being indexed. The tool now handles multiple SDKs gracefully by selecting the latest version.

### 2. Path Resolution
**Gap**: While the tool supports absolute paths, relative paths are resolved against the *current working directory* of the executable.
**Gotcha**: When running from a different directory than the solution, `git diff` and file path normalization must be carefully handled. The current implementation uses `Directory.GetCurrentDirectory()` for Git operations, which assumes you are at the repo root.
**Recommendation**: Use the solution file's directory as the base for relative path resolution and Git operations.

### 3. Authentication & Security
**Gap**: Only Basic Authentication is currently supported.
**Gotcha**: Production Neo4j instances might use LDAP, SSO, or Kerberos. Passing passwords via CLI arguments can also leak them in process lists.
**Recommendation**: Support environment variables for sensitive parameters like `--pass`. Support for environment variables for all options is planned.

### 4. Database Schema Migrations
**Gap**: The tool runs `EnsureSchemaAsync` on every run.
**Gotcha**: While it uses `IF NOT EXISTS`, any *changes* to the schema (e.g., adding a new index) require manual update of `Schema.cypher`. It doesn't handle destructive migrations or schema versioning.
**Recommendation**: Implement a simple schema versioning mechanism if the graph model evolves.

### 5. Error Handling & Resilience
**Gap**: Network flakiness during Neo4j ingestion can cause the tool to crash.
**Gotcha**: Large solutions might fail halfway through.
**Recommendation**: Implement retry logic for Neo4j operations and better logging for failed symbol extractions.

### 6. Performance with Large Repos
**Gap**: Batching is implemented, but the entire solution is loaded into memory via Roslyn.
**Gotcha**: Very large solutions (thousands of projects) might exceed memory limits in standard CI runners (e.g., 7GB on GitHub hosted runners).
**Recommendation**: Consider processing projects one-by-one or in smaller groups if memory becomes an issue.
