#!/usr/bin/env bash
# Fase 15, T9: gera web/src/generated/api-types.ts a partir do documento OpenAPI real
# (/openapi/v1.json) da API — nunca à mão, pra tipo do cliente nunca driftar do contrato do
# servidor. Uso:
#   bash scripts/generate-web-types.sh          # regenera e sobrescreve o arquivo committed
#   bash scripts/generate-web-types.sh --check   # regenera num temp e falha (exit 1) se diferir
#                                                 # do committed — é o gate anti-drift do verify.sh
set -euo pipefail
cd "$(dirname "$0")/.."

CHECK=0
[ "${1:-}" = "--check" ] && CHECK=1

PORT=52890
OUT="web/src/generated/api-types.ts"
mkdir -p "$(dirname "$OUT")"

dotnet build src/LivingWorld.Api --nologo >/dev/null

dotnet run --no-build --project src/LivingWorld.Api --urls "http://localhost:$PORT" >/tmp/livingworld-openapi.log 2>&1 &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true' EXIT

for _ in $(seq 1 60); do
  curl -sf "http://localhost:$PORT/openapi/v1.json" -o /tmp/livingworld-openapi.json 2>/dev/null && break
  sleep 0.5
done
curl -sf "http://localhost:$PORT/openapi/v1.json" -o /tmp/livingworld-openapi.json \
  || { echo "generate-web-types: API não respondeu /openapi/v1.json a tempo" >&2; cat /tmp/livingworld-openapi.log >&2; exit 1; }

if [ "$CHECK" = 1 ]; then
  TMP_OUT="$(mktemp)"
  npx --yes openapi-typescript /tmp/livingworld-openapi.json -o "$TMP_OUT" >/dev/null
  if ! diff -q "$TMP_OUT" "$OUT" >/dev/null 2>&1; then
    echo "generate-web-types --check: $OUT está desatualizado em relação ao OpenAPI atual da API." >&2
    diff "$OUT" "$TMP_OUT" >&2 || true
    echo "Rode: bash scripts/generate-web-types.sh" >&2
    rm -f "$TMP_OUT"
    exit 1
  fi
  rm -f "$TMP_OUT"
  echo "generate-web-types --check: OK, sem drift"
else
  npx --yes openapi-typescript /tmp/livingworld-openapi.json -o "$OUT" >/dev/null
  echo "generate-web-types: $OUT atualizado"
fi
