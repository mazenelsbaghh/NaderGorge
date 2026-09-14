#!/usr/bin/env bash
set -euo pipefail
cp -a /opt/cache/npm /tmp/repair-npm-cache
export npm_config_cache=/tmp/repair-npm-cache
# Dependencies are baked into the reviewed agent image; verification is offline.
dotnet restore backend/NaderGorge.sln --ignore-failed-sources
dotnet build backend/NaderGorge.sln --no-restore
dotnet test backend/NaderGorge.sln --no-build --logger 'trx;LogFileName=repair.trx'
npm --prefix frontend ci --offline --ignore-scripts
npm --prefix frontend run lint
npm --prefix frontend run build
npm --prefix worker ci --offline --ignore-scripts
npm --prefix worker test
