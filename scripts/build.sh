#!/usr/bin/env bash
# Build da solution. Uso: bash scripts/build.sh
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f LivingWorld.sln ] || { echo "LivingWorld.sln não existe — rode a Fase 0 do ROADMAP.md" >&2; exit 1; }
dotnet build LivingWorld.sln -c Release --nologo -warnaserror
