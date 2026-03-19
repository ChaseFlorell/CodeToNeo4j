#!/usr/bin/env bash
set -euo pipefail

exempt_senders="dependabot"

for s in $exempt_senders; do
  if [ "$SENDER" = "$s" ] || echo "$SENDER" | grep -qi "^${s}"; then
    echo "Sender '$SENDER' is exempt from rebased checkbox. Skipping."
    exit 0
  fi
done

if ! echo "$BODY" | grep -qF -- '- [x] Rebased on top of main'; then
  echo "::error::The 'Rebased on top of main' checkbox must be checked before merging."
  exit 1
fi
echo "Rebased checkbox is checked."