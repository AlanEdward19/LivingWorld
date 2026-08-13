#!/usr/bin/env bash
# Build da solution. Uso: bash scripts/build.sh
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f LivingWorld.sln ] || { echo "LivingWorld.sln não existe — rode a Fase 0 do ROADMAP.md" >&2; exit 1; }
dotnet build LivingWorld.sln -c Release --nologo -warnaserror

# Fase 15, T8: cliente web (tsc -b + vite build) — só roda se o projeto existir.
# Fase 15.1, T27: `npm install --prefix web` (em vez de `cd web && npm install`) resolve
# package.json a partir do cwd em vez do prefix a partir do npm 11 — sem o `cd`, o comando falha
# com ENOENT procurando um package.json na raiz do repo. `npm run` não tem esse problema, só ficou
# como estava.
if [ -f web/package.json ]; then
  (cd web && npm install --no-audit --no-fund >/dev/null)
  npm --prefix web run build
fi
