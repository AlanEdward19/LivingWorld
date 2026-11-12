#!/usr/bin/env bash
set -uo pipefail
cd /Users/aoliveira/Desktop/Projects/LivingWorld
S=/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad
bash scripts/civ-report.sh scenarios/default.json 1 20 10 $S/civ-final.json >/dev/null 2>&1
python3 - <<PY
import json
b='tests/baselines/civilization-diversity.json'
d=json.load(open(b)); r=json.load(open('$S/civ-final.json'))['summary']['diversity']
d['Gate']['Index']=r['index']
for k in d['Gate']['ByMetric']: d['Gate']['ByMetric'][k]=r['byMetric'][k]
json.dump(d,open(b,'w'),indent=2,ensure_ascii=False); open(b,'a').write('\n')
PY
LW_RECORD_HISTORY_COLLAPSE=1 bash scripts/test.sh --scenarios --filter "FullyQualifiedName~Record_history_collapse_when_explicitly_requested" 2>&1 | grep -E "Passed!|Failed!"
LW_RECORD_SCALE_STORAGE=1 bash scripts/test.sh --project longrunning --heavy --filter "FullyQualifiedName~Record_scale_storage_baseline" 2>&1 | grep -E "Passed!|Failed!"
LW_RECORD_FAMILY_DIVERSITY=1 bash scripts/test.sh --project longrunning --scenarios --filter "FullyQualifiedName~Record_explicit_family_diversity_baseline" 2>&1 | grep -E "Passed!|Failed!"
bash scripts/lint.sh --fix >/dev/null 2>&1
( time bash scripts/verify.sh --full ) > $S/verify-full.log 2>&1
echo "exit=$?" >> $S/verify-full.log
grep -E "Passed!|Failed!|verify:|exit=|^real|^\s+Failed " $S/verify-full.log | cut -c1-230; git status --short | wc -l
