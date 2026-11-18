#!/usr/bin/env bash
# Medida de FPS do cliente (ADR-0047): sobe uma API própria (porta 5291, período "cidade-media", seed 2), roda
# 3x `stress:500` (medida antiga, com vsync), 3x `bench:500` (sem vsync, cidade real aberta, ficha aberta,
# FPS médio e tempo de quadro p95) e 3x `benchbig:500` (a mesma cidade com 40 prédios a mais e, no cliente com a projeção
# do observável, lavouras, animais e corpos pelo estado). AD-188: depois, com o mundo RODANDO (40 ticks/s; rota, delta de rua e
# tempo do motor chegando), 3x `benchrun` (= `bench:500`); antes, com o mundo ainda pausado e se o cliente tiver o passo, 3x
# `benchrain` (o `bench:500` com chuva forte sintética no cliente: custo das partículas e do chão molhado). Uso: bash client/godot/tools/bench.sh [rodadas]
# Rode pela fila pesada: bash .specs/features/realism-backlog/relatorios/lw-um-por-vez.sh bash client/godot/tools/bench.sh
set -euo pipefail
cd "$(dirname "$0")/../../.."
ROUNDS="${1:-3}"
GODOT="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
PORT="${LW_BENCH_PORT:-5291}"
CLIENT="${LW_BENCH_CLIENT:-client/godot}" ## outra cópia do cliente (ex.: a versão de antes) para comparar
WORK="$(mktemp -d)"
API_PID=""
trap '[ -n "$API_PID" ] && kill "$API_PID" 2>/dev/null; rm -rf "$WORK"' EXIT
dotnet build src/LivingWorld.Api -c Release -o "$WORK/api" -v q -nologo >/dev/null
ADMIN_TOKEN=bench ConnectionStrings__World="Data Source=$WORK/lw.db" TICK_LOOP_ENABLED=true \
  dotnet "$WORK/api/LivingWorld.Api.dll" --urls "http://localhost:$PORT" >"$WORK/api.log" 2>&1 &
API_PID=$!
for _ in $(seq 1 90); do curl -fs "http://localhost:$PORT/simulation/status" >/dev/null 2>&1 && break; sleep 1; done
curl -fs "http://localhost:$PORT/periods/cidade-media" | python3 -c '
import json, sys
body = json.load(sys.stdin)
scenario = body["periodDefinition"]
if isinstance(scenario, str): scenario = json.loads(scenario)
scenario["Seed"] = 2
print(json.dumps({"scenarioJson": json.dumps(scenario), "name": "bench", "periodId": "cidade-media"}))' >"$WORK/create.json"
curl -fs -X POST -H 'Content-Type: application/json' --data @"$WORK/create.json" "http://localhost:$PORT/worlds/create" >/dev/null
"$GODOT" --headless --path "$CLIENT" --editor --import --quit >/dev/null 2>&1 || true
export LW_API_URL="http://localhost:$PORT"
ONLY="${LW_BENCH_ONLY:-}" ## "run": só a medida com o mundo rodando (diagnóstico)
paused_rounds() { [ "$ONLY" = run ] || seq 1 "$ROUNDS"; } ## (o `seq 1 0` do macOS conta para trás)
for _ in $(paused_rounds); do
  "$GODOT" --path "$CLIENT" -- --steps 'wait:2;stress:500;quit' 2>&1 | grep '^stress:' || echo 'stress: falhou'
done
for _ in $(paused_rounds); do
  "$GODOT" --path "$CLIENT" -- --steps 'wait:3;city;wait:3;bench:500;quit' 2>&1 | grep -E '^bench(:|-cost:)' || echo 'bench: falhou'
done
for _ in $(paused_rounds); do
  "$GODOT" --path "$CLIENT" -- --steps 'wait:3;city;wait:3;benchbig:500;quit' 2>&1 | grep '^benchbig:' || echo 'benchbig: falhou'
done
# AD-188: chuva forte sintética no cliente (mundo ainda pausado, compara com `bench`).
if grep -q '"rain"' "$CLIENT/Scenes/dev_steps.gd" 2>/dev/null; then
  for _ in $(paused_rounds); do
    "$GODOT" --path "$CLIENT" -- --steps 'wait:3;city;wait:3;rain:heavy;bench:500;quit' 2>&1 | grep '^bench:' | sed 's/^bench:/benchrain:/' || echo 'benchrain: falhou'
  done
fi
# AD-188: mundo rodando (antes ficava pausado: sem rota nem delta de rua).
curl -fs -X POST -H 'X-Admin-Token: bench' -H 'Content-Type: application/json' --data "{\"ticksPerSecond\":${LW_BENCH_TPS:-40}}" "http://localhost:$PORT/simulation/speed" >/dev/null
curl -fs -X POST -H 'X-Admin-Token: bench' "http://localhost:$PORT/simulation/resume" >/dev/null
sleep 5
for _ in $(seq 1 "$ROUNDS"); do
  "$GODOT" --path "$CLIENT" -- --steps "${LW_BENCH_RUN_STEPS:-wait:3;city;wait:3;bench:500;quit}" 2>&1 | grep -E '^bench(:|-cost:)' | sed 's/^bench:/benchrun:/' || echo 'benchrun: falhou'
done
