#!/usr/bin/env bash
# Perfil por sistema do tick (#158): onde vão o tempo e os bytes, e os contadores de despertar.
# Uso: bash scripts/profile.sh [--pop N] [--ticks N]   (padrão: 1000 NPCs, 720 ticks, fixture de escala, seed 42)
# Não é gate. Para comparar antes/depois com mediana use scripts/compare-simulation-cost.sh.
set -euo pipefail
cd "$(dirname "$0")/.."
POP=1000; TICKS=720
while [ $# -gt 0 ]; do case "$1" in
  --pop) POP="${2:?--pop exige um número}"; shift;;
  --ticks) TICKS="${2:?--ticks exige um número}"; shift;;
  *) echo "arg desconhecido: $1" >&2; exit 2;;
esac; shift; done
OUT="$(mktemp)"
trap 'rm -f "$OUT" "$OUT.log"' EXIT
if ! LW_PROFILE_OUT="$OUT" LW_PROFILE_POP="$POP" LW_PROFILE_TICKS="$TICKS" \
    bash scripts/test.sh --project longrunning --heavy --filter "FullyQualifiedName~SystemProfileHarness" >"$OUT.log" 2>&1; then
  tail -30 "$OUT.log" >&2
  exit 1
fi
cat "$OUT"
