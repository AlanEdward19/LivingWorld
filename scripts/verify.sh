#!/usr/bin/env bash
# Eval gate: docs + build + lint + test rápido. Saída 0 = pode concluir. Uso: bash scripts/verify.sh [--full]
#   --full  também roda os cenários pesados (Category=Scenario) do projeto Unit — nightly/antes de fechar fase.
# O cliente web foi aposentado do gate (migração para Godot): build/test/tipos TS não rodam mais aqui.
set -euo pipefail
cd "$(dirname "$0")"
bash check-docs.sh
bash build.sh
bash lint.sh
bash test.sh
[ "${1:-}" = "--full" ] && bash test.sh --scenarios
echo "verify: OK"
