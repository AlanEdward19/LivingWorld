#!/usr/bin/env bash
# Build da solution. Uso: bash scripts/build.sh
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f LivingWorld.sln ] || { echo "LivingWorld.sln não existe — rode a Fase 0 do ROADMAP.md" >&2; exit 1; }
dotnet build LivingWorld.sln -c Release --nologo -warnaserror

# Fase 15, T8: cliente web (tsc -b + vite build) — só roda se o projeto existir.
if [ -f web/package.json ]; then
  npm --prefix web install --no-audit --no-fund >/dev/null
  npm --prefix web run build
fi
