#!/usr/bin/env bash
# Sobe a API (mundo vivo, tick loop ligado) e abre o cliente Godot no menu principal.
# Uso: bash scripts/play.sh [--api-only]   (Ctrl+C encerra os dois)
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
DB="${LW_DB:-/tmp/lw-play.db}"
PORT=5214
ConnectionStrings__World="Data Source=$DB" TICK_LOOP_ENABLED=true \
  dotnet run --project src/LivingWorld.Api -c Release --urls "http://localhost:$PORT" &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true' EXIT
if [ "${1:-}" = "--api-only" ]; then wait "$API_PID"; exit 0; fi
[ -x "$GODOT" ] || { echo "Godot não encontrado: defina GODOT_BIN" >&2; exit 2; }
for _ in $(seq 1 60); do
  curl -fs "http://localhost:$PORT/simulation/status" >/dev/null 2>&1 && break
  sleep 1
done
"$GODOT" --path client/godot
