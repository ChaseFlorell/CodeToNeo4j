#!/usr/bin/env bash
set -euo pipefail

dotnet build --no-restore -c Release /p:Version="$VERSION"