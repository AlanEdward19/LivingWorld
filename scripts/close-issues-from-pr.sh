#!/usr/bin/env bash
# Fecha as issues citadas com palavra de fechamento no corpo de um PR. O GitHub só faz isso sozinho quando o merge cai na branch
# padrão (main); aqui o trabalho integra em develop, então o workflow `close-issues.yml` chama este script.
# Uso: PR_BODY="..." bash scripts/close-issues-from-pr.sh --list           # só imprime os números (testável, sem rede)
#      PR_BODY="..." PR_NUMBER=94 GH_TOKEN=... bash scripts/close-issues-from-pr.sh   # fecha de verdade
# Reconhece "Closes #1, #2 and #3", "Closes #1 e #2", "Fixes: #4", "resolved #5" (close/fix/resolve em qualquer flexão, sem diferenciar maiúsculas).
set -euo pipefail

body="${PR_BODY:-}"
numbers=$(printf '%s\n' "$body" \
  | grep -oiE '\b(close[sd]?|fix(e[sd])?|resolve[sd]?):?[[:space:]]+#[0-9]+([[:space:]]*(,|and|e|\&)[[:space:]]*#[0-9]+)*' \
  | grep -oE '#[0-9]+' | tr -d '#' | sort -un || true)

if [ "${1:-}" = "--list" ]; then
  printf '%s\n' $numbers
  exit 0
fi

[ -n "$numbers" ] || { echo "close-issues: nenhuma issue com palavra de fechamento no corpo do PR"; exit 0; }
: "${PR_NUMBER:?PR_NUMBER é obrigatório}"

for n in $numbers; do
  state=$(gh issue view "$n" --json state --jq .state 2>/dev/null || echo MISSING)
  if [ "$state" != "OPEN" ]; then echo "close-issues: #$n já está $state, nada a fazer"; continue; fi
  gh issue close "$n" --reason completed --comment "Fechada automaticamente: o PR #$PR_NUMBER foi mergeado em \`develop\` e cita esta issue." >/dev/null
  echo "close-issues: #$n fechada pelo PR #$PR_NUMBER"
done
