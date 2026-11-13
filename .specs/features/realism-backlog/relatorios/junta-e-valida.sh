#!/usr/bin/env bash
# Junção validada: regrava goldens e baselines, roda o gate completo e imprime o tempo de cada passo.
cd /Users/aoliveira/Desktop/Projects/LivingWorld
S=/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad
rm -rf $S/trx
bash scripts/refresh-world-hashes.sh 2>&1 | tail -1
echo "tempo: refresh-world-hashes terminou aos ${SECONDS}s"
bash $S/record-and-verify.sh 2>&1 | tail -24
echo "tempo: tudo terminou aos ${SECONDS}s"
grep -E "REPROVOU|^\s+Failed |Error Message" -A5 $S/verify-full.log | cut -c1-260 | head -60
