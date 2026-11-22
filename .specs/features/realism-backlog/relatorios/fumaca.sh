#!/usr/bin/env bash
# Fumaça: cliente Godot real contra um mundo RODANDO; procura divergência de contrato.
set -uo pipefail
cd /Users/aoliveira/Desktop/Projects/LivingWorld
GODOT="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
PORT=5293; WORK="$(mktemp -d)"; API_PID=""
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
print(json.dumps({"scenarioJson": json.dumps(scenario), "name": "smoke", "periodId": "cidade-media"}))' >"$WORK/create.json"
curl -fs -X POST -H 'Content-Type: application/json' --data @"$WORK/create.json" "http://localhost:$PORT/worlds/create" >/dev/null
for h in "Authorization: Bearer bench" "X-Admin-Token: bench"; do
  curl -s -o /dev/null -w "speed[$h]=%{http_code}\n" -X POST -H "$h" -H 'Content-Type: application/json' --data '{"ticksPerSecond":20}' "http://localhost:$PORT/simulation/speed"
  curl -s -o /dev/null -w "resume[$h]=%{http_code}\n" -X POST -H "$h" "http://localhost:$PORT/simulation/resume"
done
"$GODOT" --headless --path client/godot --editor --import --quit >/dev/null 2>&1 || true
export LW_API_URL="http://localhost:$PORT"
for steps in 'wait:25;log;quit' 'wait:3;city;wait:25;log;quit'; do
  echo "== $steps"
  "$GODOT" --headless --path client/godot -- --steps "$steps" 2>&1 | grep -E "^log:|contrato|fora do contrato|SCRIPT ERROR|Invalid|Parser Error" | cut -c1-400 | head -20
done
curl -s "http://localhost:$PORT/simulation/status" | cut -c1-300; echo
