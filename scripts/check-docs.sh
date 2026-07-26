#!/usr/bin/env bash
# Sensor barato: nenhum doc/rule/spec passa de 100 linhas. Uso: bash scripts/check-docs.sh
set -euo pipefail
cd "$(dirname "$0")/.."
LIMIT=100
fail=0
while IFS= read -r f; do
  n=$(wc -l < "$f")
  if [ "$n" -gt "$LIMIT" ]; then
    echo "check-docs: $f tem $n linhas (teto $LIMIT) — quebre em .md menores + índice" >&2
    fail=1
  fi
done < <(find . AGENTS.md ROADMAP.md STATE.md -maxdepth 0 -name '*.md' 2>/dev/null; \
         find rules docs -name '*.md' 2>/dev/null)
[ "$fail" = 0 ] && echo "check-docs: OK"
exit $fail
