#!/usr/bin/env bash
# Três medições comparáveis de 500 NPCs. Uso: bash scripts/client-stress.sh [caminho-do-cliente]
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
PROJECT="${1:-client/godot}"
[ -x "$GODOT" ] || { echo 'Godot não encontrado' >&2; exit 2; }
"$GODOT" --headless --path "$PROJECT" --editor --import --quit >/dev/null 2>&1
for run in 1 2 3; do
  "$GODOT" --path "$PROJECT" -- --steps 'wait:2;stress:500;quit'
done
