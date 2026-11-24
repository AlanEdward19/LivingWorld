#!/usr/bin/env bash
# Três medições comparáveis de 500 NPCs. Uso: bash scripts/client-stress.sh [caminho-do-cliente]
# O passo `stress` só começa depois do primeiro snapshot: precisa de uma API no ar (LW_API_URL, padrão
# http://localhost:5000). Cada rodada tem limite de tempo (LW_GODOT_LIMIT_S, padrão 180 s) para nunca
# prender a fila de comandos da máquina; rodada estourada sai com código 142 e é registrada como falha.
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
PROJECT="${1:-client/godot}"
API="${LW_API_URL:-http://localhost:5000}"
LIMIT="${LW_GODOT_LIMIT_S:-180}"
[ -x "$GODOT" ] || { echo 'Godot não encontrado' >&2; exit 2; }
curl -fs "$API/simulation/status" >/dev/null 2>&1 || { echo "API fora do ar em $API (suba uma e exporte LW_API_URL)" >&2; exit 3; }
"$GODOT" --headless --path "$PROJECT" --editor --import --quit >/dev/null 2>&1
for run in 1 2 3; do
  perl -e 'alarm shift; exec @ARGV' "$LIMIT" "$GODOT" --path "$PROJECT" -- --steps 'wait:2;stress:500;quit' \
    || echo "stress: rodada $run FALHOU (código $?, limite ${LIMIT}s)" >&2
done
