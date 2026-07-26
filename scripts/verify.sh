#!/usr/bin/env bash
# Eval gate: docs + build + lint + test. Saída 0 = pode concluir. Uso: bash scripts/verify.sh
set -euo pipefail
cd "$(dirname "$0")"
./check-docs.sh
./build.sh
./lint.sh
./test.sh
echo "verify: OK"
