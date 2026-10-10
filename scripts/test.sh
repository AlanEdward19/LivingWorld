#!/usr/bin/env bash
# Testes. Uso: bash scripts/test.sh [--watch] [--filter <padrão>] [--all|--scenarios] [--project <unit|integration|longrunning>]
#
# Padrão = laço rápido: LivingWorld.Tests.Unit sem `Category=Scenario` (~1 min, Release).
#   --scenarios   só os testes pesados (multi-seed / vários anos / processos separados)
#   --all         rápidos + pesados
#   --project     integration (host/DB/e2e) ou longrunning (100 anos, golden hashes) — sempre pesados
# Release de propósito: reaproveita o build de `build.sh` (sem recompilar) e a simulação roda ~2x mais rápido.
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f LivingWorld.sln ] || { echo "LivingWorld.sln não existe — rode a Fase 0 do ROADMAP.md" >&2; exit 1; }

WATCH=0; FILTER=""; MODE="fast"; PROJECT="unit"
while [ $# -gt 0 ]; do case "$1" in
  --watch) WATCH=1;;
  --filter) FILTER="${2:?--filter exige um padrão}"; shift;;
  --all) MODE="all";;
  --scenarios) MODE="scenarios";;
  --project) PROJECT="${2:?--project exige unit|integration|longrunning}"; shift;;
  *) echo "arg desconhecido: $1" >&2; exit 2;;
esac; shift; done

case "$PROJECT" in
  unit) PROJ=tests/LivingWorld.Tests.Unit;;
  integration) PROJ=tests/LivingWorld.Tests.Integration; [ "$MODE" = fast ] && MODE=all;;
  longrunning) PROJ=tests/LivingWorld.Tests.LongRunning; [ "$MODE" = fast ] && MODE=all;;
  *) echo "projeto desconhecido: $PROJECT" >&2; exit 2;;
esac

case "$MODE" in
  fast) TRAIT='Category!=Scenario';;
  scenarios) TRAIT='Category=Scenario';;
  all) TRAIT='';;
esac
if [ -n "$TRAIT" ] && [ -n "$FILTER" ]; then COMBINED="($TRAIT)&($FILTER)"
elif [ -n "$TRAIT" ]; then COMBINED="$TRAIT"
else COMBINED="$FILTER"; fi

ARGS=(-c Release --nologo)
[ -n "$COMBINED" ] && ARGS+=(--filter "$COMBINED")
if [ "$WATCH" = 1 ]; then
  exec dotnet watch --project "$PROJ" test "${ARGS[@]}"
fi
dotnet test "$PROJ" "${ARGS[@]}"
