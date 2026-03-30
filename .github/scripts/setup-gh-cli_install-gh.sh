#!/usr/bin/env bash
set -euo pipefail

if ! command -v gh &>/dev/null; then
  echo "gh CLI not found. Installing via Homebrew..."
  brew install gh
else
  echo "gh CLI is already installed: $(gh --version | head -1)"
fi
