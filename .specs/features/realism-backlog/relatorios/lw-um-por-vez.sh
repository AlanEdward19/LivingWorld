#!/usr/bin/env bash
# Fila dos comandos de simulação do LivingWorld nesta máquina, entre todos os agentes.
# Pesado (gate, medição de 20 seeds, perfil, regravar goldens): um por vez, sozinho.
#   bash lw-um-por-vez.sh <comando> [args...]
# Leve (até ~2 núcleos: sonda de uma seed, starvation-trace, testes filtrados de uma área): dois por vez.
#   bash lw-um-por-vez.sh --leve <comando> [args...]
# Leve não roda junto com pesado, e não fura a fila de um pesado que já está esperando.
# Trava de quem morreu é removida sozinha.
set -uo pipefail
A="${LW_LOCK_DIR:-/private/tmp/lw-pesado.lock}"; B="$A.2"; QUER="$A.quer"
LEVE=0; [ "${1:-}" = "--leve" ] && { LEVE=1; shift; }
CMD="$*"
dono() { cat "$1/pid" 2>/dev/null || true; }
pega() {
  if mkdir "$1" 2>/dev/null; then echo $$ > "$1/pid"; echo "$CMD" > "$1/cmd"; date > "$1/desde"; return 0; fi
  local p; p="$(dono "$1")"
  if [ -n "$p" ] && ! kill -0 "$p" 2>/dev/null; then rm -rf "$1"; fi
  return 1
}
solta() { local d; for d in "$A" "$B" "$QUER"; do [ "$(dono "$d")" = "$$" ] && rm -rf "$d"; done; return 0; }
trap solta EXIT
trap 'exit 130' INT TERM
if [ "$LEVE" = 1 ]; then
  while :; do
    if [ -d "$QUER" ]; then
      p="$(dono "$QUER")"
      if [ -n "$p" ] && ! kill -0 "$p" 2>/dev/null; then rm -rf "$QUER"; continue; fi
    elif pega "$B" || pega "$A"; then break
    fi
    sleep 5
  done
else
  until pega "$QUER"; do sleep 15; done
  until pega "$A"; do sleep 15; done
  until pega "$B"; do sleep 5; done
  rm -rf "$QUER"
fi
"$@"
