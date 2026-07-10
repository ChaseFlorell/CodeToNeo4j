#!/usr/bin/env bash
set -euo pipefail

body=$(gh pr view "$PR_NUMBER" --json body --jq '.body // ""')
if ! printf '%s\n' "$body" | grep -qE "#[0-9]+"; then
  echo "::error::PR body must reference an issue number (e.g. '#42')."
  exit 1
fi
echo "Issue reference found."
