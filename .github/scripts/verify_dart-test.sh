#!/usr/bin/env bash
set -euo pipefail

dart pub get
dart test --coverage=coverage --reporter=compact --file-reporter=json:dart-test-results.json
dart pub run coverage:format_coverage --lcov --in=coverage --out=coverage/lcov.info --report-on=lib