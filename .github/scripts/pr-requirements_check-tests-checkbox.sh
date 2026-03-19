#!/usr/bin/env bash
set -euo pipefail

exempt_senders="renovate dependabot"
exempt_labels="breaking-change build chore ci dependencies dependency skip-changelog"

for s in $exempt_senders; do
  if [ "$SENDER" = "$s" ] || echo "$SENDER" | grep -qi "^${s}"; then
    echo "Sender '$SENDER' is exempt from tests checkbox. Skipping."
    exit 0
  fi
done

label_names=$(echo "$LABELS" | jq -r '[.[].name] | join(" ")')
for l in $exempt_labels; do
  if echo " $label_names " | grep -qF " $l "; then
    echo "Label '$l' is exempt from tests checkbox. Skipping."
    exit 0
  fi
done

if ! echo "$BODY" | grep -qF -- '- [x] Tests have been added or updated'; then
  echo "::error::The 'Tests have been added or updated' checkbox must be checked before merging."
  exit 1
fi
echo "Tests checkbox is checked."