#!/usr/bin/env bash
# AD-197r: FPS em 3ª pessoa PAREADO. Os dois braços (cliente de antes e cliente de agora) encarnam o MESMO corpo, na mesma API
# em execução (mundo rodando a 40 ticks/s), e saem do personagem ao fim de cada rodada (`unplayer`); sem isso a rodada seguinte
# encarnaria outro corpo (encarnar o mesmo dá 409) e antes/depois deixaria de ser o mesmo morador. Rodadas intercaladas
# (antes, depois, antes, depois...) para o ruído da máquina cair nos dois lados.
# Uso: bash client/godot/tools/bench-third-paired.sh <cliente-de-antes> [cliente-de-agora=client/godot] [rodadas=3]
# Rode pela fila pesada: bash .specs/features/realism-backlog/relatorios/lw-um-por-vez.sh bash client/godot/tools/bench-third-paired.sh ...
# O cliente de antes precisa dos passos `player`, `unplayer` e `bench` (copie `Scenes/dev_steps.gd` de agora para a cópia antiga).
set -euo pipefail
cd "$(dirname "$0")/../../.."
BEFORE="${1:?informe o cliente de antes (pasta com project.godot)}"
AFTER="${2:-client/godot}"
ROUNDS="${3:-3}"
GODOT="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
PORT="${LW_BENCH_PORT:-5293}"
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
print(json.dumps({"scenarioJson": json.dumps(scenario), "name": "paired", "periodId": "cidade-media"}))' >"$WORK/create.json"
curl -fs -X POST -H 'Content-Type: application/json' --data @"$WORK/create.json" "http://localhost:$PORT/worlds/create" >/dev/null
for client in "$BEFORE" "$AFTER"; do "$GODOT" --headless --path "$client" --editor --import --quit >/dev/null 2>&1 || true; done
export LW_API_URL="http://localhost:$PORT"
# o corpo único das duas rodadas: o morador do meio da primeira cidade
read CITY NPC < <(python3 - "$PORT" <<'PY'
import json, sys, urllib.request
port = sys.argv[1]
def get(path):
    with urllib.request.urlopen(f"http://localhost:{port}{path}") as r: return json.load(r)
world = get("/visual/subscribe?scope=World&mode=Spectator")
cid = world["payload"]["cities"][0]["id"]["value"]
npcs = get(f"/visual/subscribe?scope=City&refId={cid}&mode=Spectator")["payload"].get("residents", [])
print(cid, npcs[len(npcs) // 2]["id"]["value"])
PY
)
echo "paired: corpo=$NPC cidade=$CITY"
curl -fs -X POST -H 'X-Admin-Token: bench' -H 'Content-Type: application/json' --data "{\"ticksPerSecond\":${LW_BENCH_TPS:-40}}" "http://localhost:$PORT/simulation/speed" >/dev/null
curl -fs -X POST -H 'X-Admin-Token: bench' "http://localhost:$PORT/simulation/resume" >/dev/null
sleep 5
STEPS="wait:3;city:$CITY;wait:3;player:$NPC;wait:2;bench:500;unplayer;wait:1;quit"
run_arm() { # <rótulo> <cliente>
  local out
  out="$( ( "$GODOT" --path "$2" -- --steps "$STEPS" 2>&1 & gp=$!; ( sleep "${LW_BENCH_WATCHDOG:-150}"; kill "$gp" 2>/dev/null ) & wd=$!; wait "$gp"; kill "$wd" 2>/dev/null ) | grep -E '^(bench:|unplayer:|incarn)' )" || true
  echo "$1 $(echo "$out" | grep '^bench:' || echo 'bench: falhou') | $(echo "$out" | grep '^unplayer:' || echo 'unplayer: sem saída')"
}
for round in $(seq 1 "$ROUNDS"); do
  run_arm antes "$BEFORE"
  run_arm depois "$AFTER"
done
