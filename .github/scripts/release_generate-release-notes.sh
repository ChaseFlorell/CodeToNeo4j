#!/usr/bin/env bash
set -euo pipefail

envsubst '${VERSION} ${SHA} ${REPOSITORY} ${RUN_ID}' \
  < ./.github/markdown/release_create-github-release.md \
  > "$RUNNER_TEMP/release-notes.md"
