#!/usr/bin/env bash
cd /Users/aoliveira/Desktop/Projects/LivingWorld
bash scripts/refresh-world-hashes.sh 2>&1 | tail -1
bash /private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/record-and-verify.sh 2>&1 | tail -16
grep -E "REPROVOU|^\s+Failed |Error Message" -A5 /private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/verify-full.log | cut -c1-260 | head -60
