#!/usr/bin/env bash
# Abre o jogo: confere .NET e Godot, compila a API só se o código mudou, sobe a API em segundo plano (ou reaproveita a do
# jogo que já está ligada), espera ficar pronta e abre o Godot no menu. Ao fechar o jogo, desliga só a API que ELE ligou.
# Uso: bash scripts/play.sh [--api-only]   ·   duplo clique: Jogar.command (raiz do projeto)
# Mundos e banco em ~/Library/Application Support/LivingWorld (macOS) ou ~/.local/share/LivingWorld; LW_DATA_DIR troca.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export PATH="$PATH:/usr/local/share/dotnet:/opt/homebrew/bin:/usr/local/bin:$HOME/.dotnet"
API_ONLY=0; [ "${1:-}" = "--api-only" ] && API_ONLY=1

say() { printf '%s\n' "$*"; }
fail() {
  printf '\n✗ %s\n' "$*" >&2
  if [ -n "${LW_PAUSE_ON_ERROR:-}" ]; then read -r -p "Aperte Enter para fechar esta janela. " _ || true; fi
  exit 1
}

case "$(uname)" in
  Darwin) DATA_DEFAULT="$HOME/Library/Application Support/LivingWorld" ;;
  *) DATA_DEFAULT="${XDG_DATA_HOME:-$HOME/.local/share}/LivingWorld" ;;
esac
DATA="${LW_DATA_DIR:-$DATA_DEFAULT}"
mkdir -p "$DATA/sessions"
STATE="$DATA/api.state" # API_PID, PORT, TOKEN e OWNER_PID da API que o jogo ligou (modo 600: o token fica só com você)
LOG="$DATA/api.log"

# --- 1. o que precisa estar instalado ----------------------------------------------------------------------------
command -v dotnet >/dev/null 2>&1 \
  || fail "Falta o .NET 10. Instale o SDK 10 em https://dotnet.microsoft.com/download e abra o jogo de novo."
dotnet --list-sdks 2>/dev/null | grep -q '^10\.' \
  || fail "O .NET instalado é antigo. Instale o SDK 10 em https://dotnet.microsoft.com/download e abra o jogo de novo."
GODOT=""
if [ "$API_ONLY" = 0 ]; then
  for candidate in "${GODOT_BIN:-}" /Applications/Godot_mono.app/Contents/MacOS/Godot /Applications/Godot.app/Contents/MacOS/Godot \
      "$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" "$HOME/Applications/Godot.app/Contents/MacOS/Godot" \
      "$(command -v godot 2>/dev/null || true)" "$(command -v godot4 2>/dev/null || true)"; do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then GODOT="$candidate"; break; fi
  done
  [ -n "$GODOT" ] || fail "Falta o Godot 4. Baixe em https://godotengine.org/download, arraste para Aplicativos e abra o jogo de novo (ou defina GODOT_BIN)."
fi

# --- 2. compilar só se o código mudou desde a última vez -----------------------------------------------------------
DLL="src/LivingWorld.Api/bin/Release/net10.0/LivingWorld.Api.dll"
changed() {
  [ ! -f "$DLL" ] && return 0
  [ -n "$(find src Directory.Build.props -newer "$DLL" -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.json' -o -name '*.props' \) \
      -not -path '*/bin/*' -not -path '*/obj/*' -print -quit 2>/dev/null)" ]
}
if changed; then
  say "Preparando o mundo (só na primeira vez ou depois de uma atualização, ~1 min)…"
  dotnet build src/LivingWorld.Api -c Release -nologo -v q >"$DATA/build.log" 2>&1 \
    || fail "Não deu para preparar o jogo. Detalhes em: $DATA/build.log"
  touch "$DLL"
fi

# --- 3. reaproveitar a API do jogo, se já estiver ligada --------------------------------------------------------------
API_PID=""; PORT=""; TOKEN=""; OWNER_PID=""
alive() { [ -n "$1" ] && kill -0 "$1" 2>/dev/null; }
is_our_api() { alive "$1" && ps -p "$1" -o command= 2>/dev/null | grep -q 'LivingWorld.Api'; }
ready() { curl -fs --max-time 2 "http://localhost:$1/simulation/status" >/dev/null 2>&1; }
write_state() {
  (umask 077; printf 'API_PID=%s\nPORT=%s\nTOKEN=%s\nOWNER_PID=%s\n' "$API_PID" "$PORT" "$TOKEN" "$OWNER_PID" >"$STATE.tmp")
  mv "$STATE.tmp" "$STATE"
}
read_state() { [ -f "$STATE" ] && grep -E '^[A-Z_]+=' "$STATE" | sed 's/^\([A-Z_]*\)=\(.*\)$/\1="\2"/' || true; }
REUSED=0
if [ -f "$STATE" ]; then
  eval "$(read_state)"
  if is_our_api "$API_PID" && ready "$PORT"; then
    REUSED=1
    say "O mundo já estava ligado (porta $PORT): vou usar o mesmo."
    if ! alive "$OWNER_PID"; then OWNER_PID=$$; write_state; fi # quem ligou já fechou: este jogo passa a cuidar dela
  else
    rm -f "$STATE"; API_PID=""; PORT=""; TOKEN=""; OWNER_PID=""
  fi
fi

# --- 4. ligar a API numa porta livre ------------------------------------------------------------------------------
busy_port() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }
if [ "$REUSED" = 0 ]; then
  for candidate in $(seq 5214 5240); do
    if ! busy_port "$candidate"; then PORT="$candidate"; break; fi
  done
  [ -n "$PORT" ] || fail "As portas 5214 a 5240 estão ocupadas por outros programas. Feche algum e abra o jogo de novo."
  [ "$PORT" = 5214 ] || say "A porta 5214 está ocupada por outro programa; o mundo vai usar a $PORT."
  TOKEN="$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')"
  printf '\n--- %s: ligando na porta %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$PORT" >>"$LOG"
  ADMIN_TOKEN="$TOKEN" ConnectionStrings__World="Data Source=$DATA/mundo.db" TICK_LOOP_ENABLED=true \
    nohup dotnet "$DLL" --urls "http://localhost:$PORT" >>"$LOG" 2>&1 &
  API_PID=$!
  OWNER_PID=$$
  write_state
  waited=0
  until ready "$PORT"; do
    alive "$API_PID" || { rm -f "$STATE"; tail -n 15 "$LOG" >&2; fail "O mundo não conseguiu ligar. Detalhes em: $LOG"; }
    [ "$waited" -ge 120 ] && { kill "$API_PID" 2>/dev/null || true; rm -f "$STATE"; fail "O mundo demorou demais para ligar. Detalhes em: $LOG"; }
    printf '\rLigando o mundo… %3d s' "$waited"
    sleep 1; waited=$((waited + 1))
  done
  printf '\rO mundo está ligado (porta %s).       \n' "$PORT"
fi

# --- 5. ao sair: desligar só a API que este jogo ligou (ou herdou), e só se ninguém mais estiver jogando --------------
touch "$DATA/sessions/$$"
cleanup() {
  rm -f "$DATA/sessions/$$"
  eval "$(read_state)"
  [ "${OWNER_PID:-}" = "$$" ] || return 0
  for other in "$DATA"/sessions/*; do
    [ -e "$other" ] || continue
    pid="$(basename "$other")"
    if alive "$pid"; then OWNER_PID="$pid"; write_state; say "Outro jogo aberto continua usando o mundo."; return 0; fi
    rm -f "$other"
  done
  if is_our_api "$API_PID"; then
    say "Desligando o mundo (fica salvo para o Continuar)…"
    kill "$API_PID" 2>/dev/null || true
    for _ in $(seq 1 20); do alive "$API_PID" || break; sleep 0.5; done
  fi
  rm -f "$STATE"
}
trap cleanup EXIT
trap 'exit 130' INT TERM
trap 'exit 129' HUP

if [ "$API_ONLY" = 1 ]; then
  say "API em http://localhost:$PORT (token de admin em $STATE). Ctrl+C desliga."
  while is_our_api "$API_PID"; do sleep 2; done
  exit 0
fi

say "Abrindo o jogo…"
LW_API_URL="http://localhost:$PORT" LW_ADMIN_TOKEN="$TOKEN" "$GODOT" --path client/godot >>"$DATA/godot.log" 2>&1 || true
