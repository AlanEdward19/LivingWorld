#!/usr/bin/env bash
# Eval gate: docs + build + lint + test. Saída 0 = pode concluir. Uso: bash scripts/verify.sh
set -euo pipefail
cd "$(dirname "$0")"
./check-docs.sh
./build.sh
./lint.sh
./test.sh
# Fase 15, T9: tipos TS do cliente web nunca driftam do contrato real da API sem reprovar o gate.
[ -f ../web/package.json ] && ./generate-web-types.sh --check
echo "verify: OK"
