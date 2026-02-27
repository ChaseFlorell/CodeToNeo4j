# CodeToNeo4j

CodeToNeo4j is a .NET tool that analyzes C# solutions and indexes their structure (projects, files, and symbols) into a Neo4j graph database.

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

## Prerequisites

- **.NET 10 SDK** must be installed on the machine.
- **Neo4j 5.0+** database.
- **Git** (if using `--diffBase`).

For more detailed documentation, visit the [GitHub Repository](https://github.com/chaseflorell/CodeToNeo4j).
