#!/usr/bin/env bash
# Demografia do cenário padrão de código (AD-130): N seeds × A anos de ScenarioRunner.Create, em paralelo.
# Uso: bash scripts/demography-report.sh [seed-inicial] [seeds] [anos] [aquecimento]   (padrão: 1 20 100 10)
# Imprime natalidade e mortalidade brutas, 1q0, e0, TFR, intervalo entre partos, razão de sexo e nascimentos por mês.
# LW_BRAIN=1 liga o cérebro com ganhos aprendidos (experimento AD-142); LW_BRAIN_BASE_FLOOR=n muda o piso do grupo base.
# Não é gate: 100 anos × 20 seeds levam dezenas de minutos.
set -euo pipefail
cd "$(dirname "$0")/.."
FIRST_SEED="${1:-1}"
SEEDS="${2:-20}"
YEARS="${3:-100}"
WARMUP="${4:-10}"
dotnet build src/LivingWorld.Workers -c Release --nologo -warnaserror -v quiet
dotnet src/LivingWorld.Workers/bin/Release/net10.0/LivingWorld.Workers.dll demography-report "$FIRST_SEED" "$SEEDS" "$YEARS" "$WARMUP"
