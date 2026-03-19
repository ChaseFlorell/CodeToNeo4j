#!/usr/bin/env bash
set -euo pipefail

label_count=$(echo "$LABELS" | jq 'length')
if [ "$label_count" -eq 0 ]; then
  echo "::error::This PR requires at least one label before it can be merged."
  exit 1
fi
echo "Labels found: $(echo "$LABELS" | jq -r '[.[].name] | join(", ")')"