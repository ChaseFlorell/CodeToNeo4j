# CodeToNeo4j

CodeToNeo4j is a .NET tool that analyzes .NET solutions and indexes their structure (projects, files, symbols, and documentation) into a Neo4j graph database.

## Features

- **Multi-File Support**: Indexes `.cs`, `.razor`, `.xaml`, `.js`, `.html`, and `.xml` files (configurable via `--include`).
- **Structural Ingestion**: Indexes Projects, Files, and Symbols (Classes, Methods, Directives, UI Elements).
- **Semantic Metadata**: Ingests XML Documentation and code comments for every symbol.
- **Incremental Indexing**: Only process changed files using `--diffBase`.
- **Accessibility Filtering**: Control which members are indexed using `--min-accessibility`.
- **Platform Native Progress**: Special progress reporting for GitHub Actions and Azure DevOps.

## Installation

Install the tool globally using NuGet:

```bash
dotnet tool install --global CodeToNeo4j
```

## Basic Usage

Run the tool by pointing it to your solution file and providing Neo4j credentials:

```bash
codetoneo4j \
  --sln ./MySolution.sln \
  --uri bolt://localhost:7687 \
  --pass your-password \
  --repoKey my-repo-id
```

## Key Options

| Option | Description |
| --- | --- |
| `--sln` | **Required**. Path to the `.sln` file to index. |
| `--repoKey` | **Required**. A unique identifier for the repository in Neo4j. |
| `--pass` | **Required**. Password for the Neo4j database. |
| `--uri` | Neo4j connection string (Default: `bolt://localhost:7687`). |
| `--user` | Neo4j username (Default: `neo4j`). |
| `--database` | Neo4j database name (Default: `neo4j`). |
| `--diffBase` | Optional git base ref (e.g., `origin/main`) for incremental indexing. |
| `--force` | Force reprocessing of the entire solution. |
| `--logLevel` | Logging verbosity (`Information`, `Debug`, etc.). |
| `--skip-dependencies` | Skip NuGet dependency ingestion. |
| `--min-accessibility` | Minimum accessibility level (e.g., `Public`, `Internal`, `Private`). |
| `--include` | File extensions to include (Default: `.cs`, `.razor`, `.xaml`, `.js`, `.html`, `.xml`). |

## Prerequisites

- **.NET 10 SDK** must be installed on the machine.
- **Neo4j 5.0+** database.
- **Git** (if using `--diffBase`).

For more detailed documentation, visit the [GitHub Repository](https://github.com/chaseflorell/CodeToNeo4j).

## CI/CD Integration

### GitHub Actions

You can install and run `CodeToNeo4j` directly in your GitHub workflows:

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    global-json-file: global.json

- name: Install CodeToNeo4j
  run: dotnet tool install --global CodeToNeo4j

- name: Run CodeToNeo4j
  run: |
    codetoneo4j \
      --sln ./MySolution.sln \
      --repoKey my-repo \
      --uri ${{ secrets.NEO4J_URL }} \
      --pass ${{ secrets.NEO4J_PASS }} \
      --diffBase HEAD~1
```

### Azure DevOps

Use the `.NET Core` task to install and run the tool:

```yaml
steps:
- task: UseDotNet@2
  inputs:
    packageType: 'sdk'
    useGlobalJson: true

- script: dotnet tool install --global CodeToNeo4j
  displayName: 'Install CodeToNeo4j'

- script: |
    codetoneo4j \
      --sln ./MySolution.sln \
      --repoKey my-repo \
      --uri $(NEO4J_URL) \
      --pass $(NEO4J_PASS) \
      --diffBase HEAD~1
  displayName: 'Run CodeToNeo4j'
```
