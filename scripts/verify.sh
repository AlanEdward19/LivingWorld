#!/usr/bin/env bash
# Eval gate: docs + build + lint + test rápido. Saída 0 = pode concluir. Uso: bash scripts/verify.sh [--full|--nightly]
#   (padrão)   gate de rotina: Unit sem `Category=Scenario` (~1 min).
#   --full     + Unit `Category=Scenario` (~1 min) + Integration (contrato da API, composição; ~1 min)
#              + LongRunning sem `Category=Heavy` (~3 min). Antes de fechar fase / abrir PR grande.
#   --nightly  --full + `Category=Heavy` (10 mil NPCs, 100x, sensor de 5 mil; ~1 min).
# O cliente web foi aposentado do gate (migração para Godot): build/test/tipos TS não rodam mais aqui.
set -euo pipefail
cd "$(dirname "$0")"
MODE="${1:-}"
case "$MODE" in ""|--full|--nightly) ;; *) echo "uso: verify.sh [--full|--nightly]" >&2; exit 2;; esac
bash check-docs.sh
bash build.sh
bash lint.sh
bash test.sh
if [ "$MODE" = "--full" ] || [ "$MODE" = "--nightly" ]; then
  bash test.sh --scenarios
  bash test.sh --project integration
  bash test.sh --project longrunning
fi
[ "$MODE" = "--nightly" ] && bash test.sh --project longrunning --heavy --filter "Category=Heavy"
echo "verify: OK"
