#!/usr/bin/env bash
# Lista diretórios de .specs/features sem estado declarado: primeira linha de spec.md/plan.md
# contém "Estado:" ou existe verification.md (formato lean, estado provado lá). Vazio = OK.
cd "$(dirname "$0")/../.specs/features" || exit 1
for d in */; do
  d=${d%/}
  [ -f "$d/verification.md" ] && continue
  ok=0
  for f in "$d/spec.md" "$d/plan.md"; do
    [ -f "$f" ] && head -n1 "$f" | grep -q 'Estado:' && ok=1
  done
  [ "$ok" = 1 ] || echo "$d"
done
