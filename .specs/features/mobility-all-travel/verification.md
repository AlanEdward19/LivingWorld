# Verificação independente — Mobilidade em todo deslocamento (#61 fatia A)

Verifier: sub-agent fresco (não escreveu o código). Diff: `develop...HEAD` (HEAD 3c058ec, 11 arquivos).
Rodada refeita do zero; sobrescreve a anterior. Árvore restaurada após cada mutação (`git checkout -- <arquivo>`).
Veredito: **PASS** (7/7 critérios; 3 observações não bloqueantes ao final).

## Critérios

| # | Critério | Veredito |
|---|---|---|
| 1 | Todo caminho de duração de `Travel` passa por `Mobility.TravelTicks` | PASS |
| 2 | Base/tratamento: menor fração em `Travel` em ≥ 18/20 seeds | PASS |
| 3 | Desligar o bônus muda o mundo em 10 anos | PASS |
| 4 | Sensor de escala ≤ 103% µs/NPC-tick e ≤ 102% alocação | PASS |
| 5 | `load`/`reach` = FUTURE_DEPENDENCY no audit | PASS |
| 6 | `default.json` com Emergence carrega; goldens passam | PASS |
| 7 | Docs ≤ 100 linhas | PASS |

## 1. Caminhos de duração
- `grep -rn "TicksBetween|TravelTicksCache|ActionType.Travel" src`: os três sítios de duração de `Travel` são
  `BehaviorDecisionSystem.Schedule.cs:27` (`TravelArrivalTick`), `:164` (`LeadHoursTo`) e `BehaviorDecisionSystem.cs:230`
  (`TryCompleteAction`); todos chamam `Mobility.TravelTicks` (`Mobility.cs:28`).
- Migração: `MigrationSystem.cs:114` só faz `SetCurrentAction(Travel)`; o destino vem de `RelocationDestinationOf`, primeiro ramo de
  `TravelDestinationOf` (`Destinations.cs:34`), logo a duração passa por `TryCompleteAction` (`:230`) e `TravelArrivalTick`.
- Restantes `TicksBetween` fora de `Travel`: `MarketIndex.cs:46,160,68` (ordenação por distância), `TravelTicksCache.cs:22`,
  `TechDiscoverySystem.cs:135` e `Schedule.cs:109` / `Effects.cs:236` (despensa/compra, já usam `Mobility.Apply` + `BonusPct`).
  `RegisterRoute`/`MovementRoute` sem chamadores em `src` (cosmético). Locomoção extraordinária avança por passo, não por tick (fora do escopo).
- Mutações (`bash scripts/test.sh --filter MobilityTravelTests`, base: 4/4 passam):
  - remover bônus em `TravelTicks` (`Apply(...)` -> `ticks`): 3 reprovam (`Travel_ticks_shrink...`, `Adopted_mobility_lowers...`, `Commute_lead_time...`).
  - `TravelArrivalTick` volta a `TicksBetween`: reprova `Commute_lead_time_and_scheduled_arrival...`.
  - `LeadHoursTo` volta a `TicksBetween`: reprova `Commute_lead_time_and_scheduled_arrival...`.
  - `TryCompleteAction` volta a `TicksBetween`: reprova `Adopted_mobility_lowers_the_share...`.

## 2. Fração em Travel (20 seeds)
`MobilityTravelTests.cs:62` (30 dias, tecnologia `[7,0,0]` adotada no tick 0, exige `lower >= 18`). Passou na base.
Reprova sob a mutação de remover o bônus e sob a de `TryCompleteAction` (acima).

## 3. Desligar o bônus muda o mundo
`MobilityTravelTests.cs:75`: mesma tecnologia no estado, `PaceBonusPctPerPoint` 0 vs padrão, 10 anos; compara rastro dos NPCs
(`Trace`) e `CanonicalHash`. Passou. Mutação `BonusPct` retorna 0: reprovam os 4 testes, inclusive este.

## 4. Sensor de escala
`bash scripts/compare-simulation-cost.sh /private/tmp/lw-develop <dir>` (worktree de `develop` 6799bf3, removido depois), saída 0:
- antes 0,804 µs/NPC-tick, 258836 B/tick; depois 0,805 µs, 258692 B/tick (medianas de 3 rodadas).
- `cpuRatio` 1,0012 (100,1%); `allocationRatio` 0,9994 (99,9%). Limites 103% / 102%.
- Séries `alive@1..10y` de `village` e `live` idênticas antes/depois.

## 5. load/reach
`docs/audits/living-world-cohesion-audit.md:29` (`pace` CAUSAL via `Mobility.TravelTicks`) e `:30` (`load`/`reach` FUTURE_DEPENDENCY).

## 6. default.json e goldens
- `scenarios/default.json:156` `"Emergence": { "Enabled": true }`.
- `bash scripts/test.sh --project integration`: 448/448 passam (sobe o mundo ao vivo do default).
- `bash scripts/test.sh --project longrunning`: 141 passam, 8 skip, 1 falha (ver observação 1); goldens/baselines de hash verdes.
- `bash scripts/test.sh --scenarios`: 20 passam, 1 skip, 0 falhas.

## 7. Docs
`bash scripts/check-docs.sh`: `check-docs: OK`. `plan.md` 35 linhas; audit 50; STATE 76; phase-24 98.

## Gate
`bash scripts/verify.sh`: saída 0, `Passed! 2458, Skipped 3`, `verify: OK`.

## Observações (não bloqueantes)
1. `EconomyDepthConservationTests.Live_world_population_stays_within_90_percent_of_the_baseline` falha em LongRunning
   (136 < 90% de 213). **Idêntico em `develop` limpo** (mesma mensagem): falha pré-existente, não causada por este diff.
   Não faz parte de `verify.sh` (só unit rápido), mas quem rodar `--full` a verá.
2. Sem teste dedicado de migração: a duração de realocação só é guardada pelo teste estatístico de fração (via `TryCompleteAction`).
3. Sob a mutação "remover bônus em `TravelTicks`" o teste do critério 3 continua verde, pois o canal da despensa/compra
   (`BonusPct`) ainda diferencia os mundos; a guarda do caminho de viagem está nos testes 1, 2 e do trajeto.
