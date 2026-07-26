#!/usr/bin/env bash
# Testes. Uso: bash scripts/test.sh [--watch] [--filter <padrão>]
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f LivingWorld.sln ] || { echo "LivingWorld.sln não existe — rode a Fase 0 do ROADMAP.md" >&2; exit 1; }

WATCH=0; FILTER=""
while [ $# -gt 0 ]; do case "$1" in
  --watch) WATCH=1;;
  --filter) FILTER="${2:?--filter exige um padrão}"; shift;;
  *) echo "arg desconhecido: $1" >&2; exit 2;;
esac; shift; done

# Cenários longos (100 anos) ficam fora do gate padrão: rode com --filter Category=Scenario
ARGS=(--nologo --filter "${FILTER:-Category!=Scenario}")
if [ "$WATCH" = 1 ]; then
  exec dotnet watch --project tests/LivingWorld.Tests test "${ARGS[@]}"
fi
dotnet test LivingWorld.sln "${ARGS[@]}"
