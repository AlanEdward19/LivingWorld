#!/usr/bin/env bash
# Lint + format. Uso: bash scripts/lint.sh [--fix]
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f LivingWorld.sln ] || { echo "LivingWorld.sln não existe — rode a Fase 0 do ROADMAP.md" >&2; exit 1; }

if [ "${1:-}" = "--fix" ]; then
  dotnet format LivingWorld.sln
else
  dotnet format LivingWorld.sln --verify-no-changes
fi
