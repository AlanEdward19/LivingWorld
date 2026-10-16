#!/usr/bin/env bash
# Atualização explícita dos goldens após mudança de comportamento registrada em ADR/AD.
set -euo pipefail
cd "$(dirname "$0")/.."
bash scripts/build.sh
python3 - <<'PY'
import json, re, subprocess
from pathlib import Path
worker = Path('src/LivingWorld.Workers/bin/Release/net10.0/LivingWorld.Workers.dll')
def hashes(seed, ticks):
    return subprocess.check_output(['dotnet', str(worker), 'hash', str(seed), str(ticks)], text=True).strip().split(';')
p = Path('tests/golden/world-hashes.json')
entries = json.loads(p.read_text())
for entry in entries:
    assert entry['Scenario'] == 'default'
    entry['CanonicalHash'] = hashes(entry['Seed'], entry['Ticks'])[0]
p.write_text(json.dumps(entries, indent=2) + '\n')
p = Path('tests/LivingWorld.Tests.Unit/Performance/OptimizationEquivalenceTests.cs')
s = p.read_text()
for seed in [1, 2]:
    canonical, volatile = hashes(seed, 90 * 24)
    s, count = re.subn(r'\[InlineData\(' + str(seed) + r'UL, "[a-f0-9]{64}", "[a-f0-9]{64}"\)\]',
                      f'[InlineData({seed}UL, "{canonical}", "{volatile}")]', s)
    assert count == 1
p.write_text(s)
print('Goldens e dois pins de 90 dias atualizados explicitamente.')
PY
