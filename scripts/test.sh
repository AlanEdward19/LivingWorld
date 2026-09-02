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

# Gate padrão roda só LivingWorld.Tests.Unit (rápido). Integration cobre host/DB/e2e curtos;
# LongRunning cobre cenários de horas/dias reais (100 anos, multi-seed) — roda sob demanda:
#   dotnet test tests/LivingWorld.Tests.Integration
#   dotnet test tests/LivingWorld.Tests.LongRunning --filter Category=Scenario
ARGS=(--nologo)
[ -n "$FILTER" ] && ARGS+=(--filter "$FILTER")
if [ "$WATCH" = 1 ]; then
  exec dotnet watch --project tests/LivingWorld.Tests.Unit test "${ARGS[@]}"
fi
dotnet test tests/LivingWorld.Tests.Unit "${ARGS[@]}"

# Fase 15, T8: cliente web tem sua própria suíte (Vitest) — sem filtro/watch por ora, o dotnet
# test acima já cobre esses casos pro lado .NET.
[ -f web/package.json ] && npm --prefix web test
