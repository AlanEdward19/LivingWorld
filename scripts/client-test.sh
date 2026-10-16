#!/usr/bin/env bash
# Testes do cliente Godot (headless). Uso: bash scripts/client-test.sh [--filter arquivo::teste]
# Fora do verify.sh (ADR-0022): o gate .NET não depende do Godot. Godot em GODOT_BIN ou /Applications/Godot_mono.app.
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
[ -x "$GODOT" ] || { echo "Godot não encontrado: defina GODOT_BIN" >&2; exit 2; }
FILTER=()
while [ $# -gt 0 ]; do case "$1" in
  --filter) FILTER=(--filter "${2:?--filter exige arquivo::teste}"); shift;;
  *) echo "arg desconhecido: $1" >&2; exit 2;;
esac; shift; done
OUT="$(mktemp)"; trap 'rm -f "$OUT"' EXIT
set +e
"$GODOT" --headless --path client/godot --script res://tests/run.gd -- ${FILTER[@]+"${FILTER[@]}"} 2>&1 | tee "$OUT"
CODE=${PIPESTATUS[0]}
set -e
# Erro de script/parse da engine não derruba o teste sozinho: trate como falha do gate.
if grep -qE "SCRIPT ERROR|Parse Error" "$OUT"; then echo "client-test: erro de script da engine" >&2; exit 1; fi
exit "$CODE"
