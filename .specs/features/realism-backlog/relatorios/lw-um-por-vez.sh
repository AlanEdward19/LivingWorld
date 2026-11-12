#!/usr/bin/env bash
# Fila de uma vaga para comandos pesados do LivingWorld nesta máquina (gate, testes, relatórios de 20 seeds, perfil).
# Uso: bash lw-um-por-vez.sh <comando> [args...]   Ex.: bash lw-um-por-vez.sh bash scripts/verify.sh --full
# Espera a vez, roda o comando e libera. Trava de quem morreu é removida sozinha.
set -uo pipefail
LOCK="${LW_LOCK_DIR:-/private/tmp/lw-pesado.lock}"
while ! mkdir "$LOCK" 2>/dev/null; do
  owner="$(cat "$LOCK/pid" 2>/dev/null || true)"
  if [ -n "$owner" ] && ! kill -0 "$owner" 2>/dev/null; then rm -rf "$LOCK"; continue; fi
  sleep 15
done
echo $$ > "$LOCK/pid"; echo "$*" > "$LOCK/cmd"; date > "$LOCK/desde"
trap 'rm -rf "$LOCK"' EXIT INT TERM
"$@"
