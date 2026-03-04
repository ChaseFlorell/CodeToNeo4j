# CodeToNeo4j

CodeToNeo4j is a .NET tool that analyzes .NET solutions and indexes their structure (projects, files, symbols, and documentation) into a Neo4j graph database.

## Features

- **Multi-File Support**: Indexes `.cs`, `.razor`, `.xaml`, `.js`, `.html`, `.xml`, `.json`, `.css`, and `.csproj` files (configurable via `--include`).
- **Structural Ingestion**: Indexes Projects, Files, and Symbols (Classes, Methods, Directives, UI Elements).
- **Semantic Metadata**: Ingests XML Documentation and code comments for every symbol.
- **Incremental Indexing**: Only process changed files using `--diff-base`. When enabled, also ingests detailed commit history (hashes, authors, messages) and links them to the modified files.
- **Git Metadata**: Tracks file metadata including creation/modification dates, commits, and individual author statistics (contribution counts and dates).
- **Administrative Tools**: Safely purge data by repository key using `--purge-data`.
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
  -sln ./MySolution.sln \
  --uri bolt://localhost:7687 \
  --password your-password
```

## Key Options

| Option                      | Description                                                                            |
|-----------------------------|----------------------------------------------------------------------------------------|
| `--sln`, `-s`               | **Required** unless using `--purge-data`. Path to the `.sln` file to index. |
| `--no-key`                  | Do not use a repository key. Use this if the Neo4j instance is dedicated to this repository. |
| `--password`, `-p`          | **Required**. Password for the Neo4j database.                                         |
| `--uri`, `-u`, `--url`      | Neo4j connection string (Default: `bolt://localhost:7687`).                            |
| `--user`                    | Neo4j username (Default: `neo4j`).                                                     |
| `--database`, `-db`         | Neo4j database name (Default: `neo4j`).                                                |
| `--diff-base`               | Optional git base ref (e.g., `origin/main`) for incremental indexing.                  |
| `--log-level`, `-l`         | Logging verbosity (`Information`, `Debug`, etc.).                                      |
| `--debug`, `-d`             | Turn on debug logging.                                                                 |
| `--verbose`, `-v`           | Turn on trace logging.                                                                 |
| `--quiet`, `-q`             | Mute all logging output.                                                               |
| `--skip-dependencies`       | Skip NuGet dependency ingestion.                                                       |
| `--min-accessibility`       | Minimum accessibility level (e.g., `Public`, `Internal`, `Private`).                   |
| `--include`, `-i`           | File extensions to include (Default: all supported).                                   |
| `--purge-data`              | Purge data associated with the repository.                                         |

> Note: When using `--purge-data`, `--sln` is not required if `--no-key` is used. The tool asks for confirmation before deletion. If `--include` is specified, only matching file extensions are purged. `--skip-dependencies` and `--min-accessibility` are not allowed with this switch. Only one of `--log-level`, `--debug`, `--verbose`, or `--quiet` can be used.

### Purge examples

- Purge by derived repository key:
  ```bash
  codetoneo4j -s ./MySolution.sln --password your-pass --purge-data
  ```
- Purge all CodeToNeo4j data (when using --no-key):
  ```bash
  codetoneo4j --no-key --password your-pass --purge-data
  ```
- Purge only certain file types:
  ```bash
  codetoneo4j -s ./MySolution.sln --password your-pass --purge-data --include .cs --include .razor
  ```

## Prerequisites

- **.NET 8, 9, or 10 SDK** must be installed on the machine.
- **Neo4j 5.0+** database.
- **Git** (if using `--diff-base`).

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
      -s ./MySolution.sln \
      --uri ${{ secrets.NEO4J_URL }} \
      --password ${{ secrets.NEO4J_PASS }} \
      --diff-base ${{ github.event.before }}
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
      -s ./MySolution.sln \
      --uri $(NEO4J_URL) \
      --password $(NEO4J_PASS) \
      --diff-base $(System.PullRequest.SourceCommitId)
  displayName: 'Run CodeToNeo4j'
```
