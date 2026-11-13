#!/usr/bin/env bash
# Eval gate: docs + build + lint + testes. Saída 0 = pode concluir. Uso: bash scripts/verify.sh [--lote|--full|--nightly]
#   (padrão)   gate de rotina: Unit sem `Category=Scenario`, em paralelo com o lint.
#   --lote     --full sem `Category=Century` (os séculos de 20 seeds): gate de cada junção de fatias. O --full roda 1x por dia.
#   --full     Unit inteiro (rápidos + `Category=Scenario`) + Integration + LongRunning sem `Category=Heavy`.
#              Antes de fechar fase / abrir PR grande.
#   --nightly  --full + `Category=Heavy` (10 mil NPCs, 100x, sensor de 5 mil).
# Um build só (build.sh); os `dotnet test` rodam com --no-build. As suítes independentes rodam em paralelo, cada uma
# com o seu log; o namespace `LongRunning.Performance` (asserts de tempo e alocação) roda depois, sozinho na máquina,
# como antes. Qualquer suíte reprovada derruba o gate e tem o log impresso. Logs: $LW_VERIFY_LOGS (padrão: dir temporário).
# O cliente web foi aposentado do gate (migração para Godot): build/test/tipos TS não rodam mais aqui.
set -euo pipefail
cd "$(dirname "$0")"
MODE="${1:-}"
case "$MODE" in ""|--lote|--full|--nightly) ;; *) echo "uso: verify.sh [--lote|--full|--nightly]" >&2; exit 2;; esac
LOGS="${LW_VERIFY_LOGS:-$(mktemp -d "${TMPDIR:-/tmp}/lw-verify.XXXXXX")}"
mkdir -p "$LOGS"

bash check-docs.sh
bash build.sh

PIDS=(); NAMES=()
trap 'for p in "${PIDS[@]:-}"; do [ -n "$p" ] && pkill -P "$p" 2>/dev/null; kill "$p" 2>/dev/null; done' INT TERM
start() { # start <nome> <script> [args...]: roda em segundo plano, saída em $LOGS/<nome>.log
  local name="$1"; shift
  ( t=$SECONDS; bash "$@"; rc=$?; echo "verify-tempo: $name $((SECONDS - t))s"; exit $rc ) > "$LOGS/$name.log" 2>&1 &
  PIDS+=($!); NAMES+=("$name")
}
wait_all() { # espera todos; imprime o resumo de cada um e o log inteiro de quem reprovou
  local fail=0 i
  for i in "${!PIDS[@]}"; do
    if wait "${PIDS[$i]}"; then
      echo "verify: ${NAMES[$i]} OK ($(grep -E '^verify-tempo:' "$LOGS/${NAMES[$i]}.log" | tail -1 | cut -d' ' -f3)) $(grep -E '(Passed|Failed)!' "$LOGS/${NAMES[$i]}.log" | tail -1 | cut -c1-100)"
    else
      cat "$LOGS/${NAMES[$i]}.log" >&2
      echo "verify: ${NAMES[$i]} REPROVOU (log: $LOGS/${NAMES[$i]}.log)" >&2
      fail=1
    fi
  done
  PIDS=(); NAMES=()
  return $fail
}

PERF='FullyQualifiedName~LivingWorld.Tests.LongRunning.Performance.'
NOT_PERF='FullyQualifiedName!~LivingWorld.Tests.LongRunning.Performance.'
if [ -z "$MODE" ]; then
  start lint lint.sh
  start unit test.sh --no-build
  wait_all
else
  # O LongRunning é o mais longo: entra primeiro; Unit, Integration e lint enchem os núcleos em volta dele.
  # LW_PREWARM_SHARED_RUNS: o século compartilhado (ADR-0044) começa no carregamento do assembly. Só nesta suíte: no
  # LongRunning.Performance ele competiria com os asserts de tempo.
  if [ "$MODE" = "--lote" ]; then
    start longrunning test.sh --project longrunning --no-build --no-century --filter "$NOT_PERF"
  else
    export LW_PREWARM_SHARED_RUNS=1
    start longrunning test.sh --project longrunning --no-build --filter "$NOT_PERF"
    unset LW_PREWARM_SHARED_RUNS
  fi
  start unit test.sh --all --no-build
  start integration test.sh --project integration --no-build
  start lint lint.sh
  wait_all
  start longrunning-performance test.sh --project longrunning --no-build --filter "$PERF"
  wait_all
fi
if [ "$MODE" = "--nightly" ]; then
  start heavy test.sh --project longrunning --no-build --heavy --filter "Category=Heavy"
  wait_all
fi
echo "verify: OK"
