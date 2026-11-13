#!/usr/bin/env bash
# Junção validada: regrava goldens e baselines, roda o gate e imprime o tempo de cada passo.
# Uso: junta-e-valida.sh [--lote|--full]  (--lote em cada junção; --full uma vez por dia, com os séculos de 20 seeds)
cd /Users/aoliveira/Desktop/Projects/LivingWorld
S=/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad
rm -rf $S/trx
bash scripts/refresh-world-hashes.sh 2>&1 | tail -1
echo "tempo: refresh-world-hashes terminou aos ${SECONDS}s"
bash $S/record-and-verify.sh "${1:---lote}" 2>&1 | tail -24
echo "tempo: tudo terminou aos ${SECONDS}s"
grep -E "REPROVOU|^\s+Failed |Error Message" -A5 $S/verify-full.log | cut -c1-260 | head -60
