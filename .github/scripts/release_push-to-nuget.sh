#!/usr/bin/env bash
set -euo pipefail

dotnet nuget push out/*.nupkg --source "https://api.nuget.org/v3/index.json" --api-key "$NUGET_KEY"