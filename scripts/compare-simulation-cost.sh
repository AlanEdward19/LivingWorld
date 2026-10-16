#!/usr/bin/env bash
# Uso: bash scripts/compare-simulation-cost.sh <checkout-base> [diretório-saída]
set -euo pipefail
cd "$(dirname "$0")/.."
BASE="${1:?informe checkout base}"
CURRENT="$PWD"
OUT_DIR="${2:-$(mktemp -d)}"
mkdir -p "$OUT_DIR"
OUT_DIR="$(cd "$OUT_DIR" && pwd)"
for round in 1 2 3; do
  for arm in before after; do
    TARGET="$CURRENT"
    [ "$arm" = before ] && TARGET="$BASE"
    (cd "$TARGET" && LW_MEASURE_OUT="$OUT_DIR/$arm-$round.txt" bash scripts/test.sh --project longrunning --filter MeasureHarness) >"$OUT_DIR/$arm-$round.log" 2>&1
  done
done
python3 - "$OUT_DIR" <<'PYCODE'
import json, re, statistics, sys
from pathlib import Path
out = Path(sys.argv[1])
result = {}
for arm in ['before', 'after']:
    samples = [tuple(float(v.replace(',', '.')) for v in re.search(r'microsPerNpcTick=([\d.,]+) allocPerTick=([\d.,]+)', f.read_text()).groups()) for f in sorted(out.glob(f'{arm}-*.txt'))]
    result[arm] = {'samples': samples, 'micros': statistics.median(s[0] for s in samples), 'allocated': statistics.median(s[1] for s in samples)}
result['cpuRatio'] = result['after']['micros'] / result['before']['micros']
result['allocationRatio'] = result['after']['allocated'] / result['before']['allocated']
(out / 'comparison.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
if result['cpuRatio'] > 1.03 or result['allocationRatio'] > 1.02:
    raise SystemExit('regressão acima de +3% CPU ou +2% alocação')
PYCODE
