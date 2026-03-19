#!/usr/bin/env bash
set -euo pipefail

body=$(gh pr view "$PR_NUMBER" --json body --jq '.body // ""')
if ! printf '%s\n' "$body" | grep -qE "^Resolves #[0-9]+"; then
  echo "::error::PR body must contain 'Resolves #<issue-number>' (e.g. 'Resolves #42')."
  exit 1
fi
echo "Issue reference found."
