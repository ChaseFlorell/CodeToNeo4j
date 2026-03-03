#!/bin/bash

# Configuration
PROJECT_PATH="src/CodeToNeo4j/CodeToNeo4j.csproj"
ARTIFACTS_DIR="artifacts"
TOOL_NAME="codetoneo4j"
VERSION="0.0.1"

# Ensure we're in the project root
cd "$(dirname "$0")"

echo "Cleaning up..."
rm -rf "$ARTIFACTS_DIR"
mkdir "$ARTIFACTS_DIR"

echo "Packing tool..."
dotnet pack "$PROJECT_PATH" -o "$ARTIFACTS_DIR" /p:Version="$VERSION" --configuration Release

echo "Uninstalling existing tool (if any)..."
dotnet tool uninstall -g "$TOOL_NAME" 2>/dev/null || true

echo "Installing tool globally from local source..."
dotnet tool install -g "$TOOL_NAME" --add-source "./$ARTIFACTS_DIR" --version "$VERSION"

echo "Done! You can now run the tool using '$TOOL_NAME'"
