#!/usr/bin/env bash
set -euo pipefail

dotnet pack src/CodeToNeo4j/CodeToNeo4j.csproj --no-restore -c Release -o out /p:Version="$VERSION"