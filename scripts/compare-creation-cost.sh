#!/usr/bin/env bash
# Compara 6000 NPCs em braços alternados. Uso: bash scripts/compare-creation-cost.sh <checkout-base> [diretório-saída]
set -euo pipefail
cd "$(dirname "$0")/.."
BASE="${1:?informe o checkout base com o mesmo WorldCreationCostTests}"
CURRENT="$PWD"
OUT_DIR="${2:-$(mktemp -d)}"
mkdir -p "$OUT_DIR"
OUT_DIR="$(cd "$OUT_DIR" && pwd)"
for round in 1 2 3; do
  for arm in before after; do
    TARGET="$CURRENT"
    [ "$arm" = before ] && TARGET="$BASE"
    (cd "$TARGET" && LW_CREATION_COST_OUT="$OUT_DIR/$arm-$round.txt" bash scripts/test.sh --project longrunning --filter WorldCreationCostTests) >"$OUT_DIR/$arm-$round.log" 2>&1
  done
done
python3 - "$OUT_DIR" <<'PY'
import json, statistics, sys
from pathlib import Path
out = Path(sys.argv[1])
result = {}
for arm in ['before', 'after']:
    samples = [float(value) for file in sorted(out.glob(f'{arm}-*.txt')) for value in file.read_text().split(',')]
    result[arm] = {'samplesMs': samples, 'medianMs': statistics.median(samples)}
result['ratio'] = result['after']['medianMs'] / result['before']['medianMs']
(out / 'comparison.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
if result['ratio'] > 1.05:
    raise SystemExit('criação excede o limite de +5% da issue #67')
PY
