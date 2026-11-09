#!/usr/bin/env bash
# Observatório de civilização (issue #112): roda N seeds por X anos e grava a série anual em JSON.
# Uso: bash scripts/civ-report.sh [cenário] [seed-inicial] [seeds] [anos] [saída]
#   padrão: scenarios/default.json 1 20 10 artifacts/civ-report.json (sem persistência; seeds em paralelo)
set -euo pipefail
cd "$(dirname "$0")/.."
SCENARIO="${1:-scenarios/default.json}"
FIRST_SEED="${2:-1}"
SEEDS="${3:-20}"
YEARS="${4:-10}"
OUT="${5:-artifacts/civ-report.json}"
[ -f "$SCENARIO" ] || { echo "cenário não encontrado: $SCENARIO" >&2; exit 2; }
dotnet build src/LivingWorld.Workers -c Release --nologo -warnaserror -v quiet
dotnet src/LivingWorld.Workers/bin/Release/net10.0/LivingWorld.Workers.dll civ-report "$SCENARIO" "$FIRST_SEED" "$SEEDS" "$YEARS" "$OUT"
