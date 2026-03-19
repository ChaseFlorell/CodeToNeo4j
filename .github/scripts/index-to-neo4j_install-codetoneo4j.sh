#!/usr/bin/env bash
set -euo pipefail

dotnet tool update --global CodeToNeo4j
echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"