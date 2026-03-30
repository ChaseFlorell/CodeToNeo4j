#!/usr/bin/env bash
set -euo pipefail

codetoneo4j \
  --sln ./CodeToNeo4j.slnx \
  --uri "$NEO4J_URI" \
  --user neo4j \
  --database CodeToNeo4j \
  --password "$NEO4J_PASSWORD" \
  --diff-base "$DIFF_BASE"
