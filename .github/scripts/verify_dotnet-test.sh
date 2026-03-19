#!/usr/bin/env bash
set -euo pipefail

rm -rf ./TestResults
dotnet test --no-build -c Release --verbosity normal --logger "trx" --results-directory ./TestResults --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover