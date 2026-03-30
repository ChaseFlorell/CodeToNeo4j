#!/usr/bin/env bash
set -euo pipefail

codetoneo4j \
  --sln ./CodeToNeo4j.slnx \
  --uri bolt://192.168.4.135:7687 \
  --user neo4j \
  --database CodeToNeo4j \
  --password "$NEO4J_PASSWORD" \
  --diff-base "$DIFF_BASE"
