## Installation

Install from Nuget:
[![NuGet](https://img.shields.io/badge/nuget-v${VERSION}-blue)](https://www.nuget.org/packages/CodeToNeo4j/${VERSION})

Install from CLI:

```bash
dotnet tool install --global CodeToNeo4j --version ${VERSION}
```

## Provenance

| Commit                                                     | Build                                                                |
|------------------------------------------------------------|----------------------------------------------------------------------|
| [`${SHA}`](https://github.com/${REPOSITORY}/commit/${SHA}) | [${RUN_ID}](https://github.com/${REPOSITORY}/actions/runs/${RUN_ID}) |

Verify package attestation:

```bash
gh attestation verify <path-to-nupkg> --repo ${REPOSITORY}
```
