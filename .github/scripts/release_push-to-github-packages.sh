#!/usr/bin/env bash
set -euo pipefail

dotnet nuget push out/*.nupkg --source "https://nuget.pkg.github.com/$REPOSITORY_OWNER/index.json" --api-key "$GITHUB_TOKEN"