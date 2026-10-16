# Verificação independente — Mobilidade em todo deslocamento (#61 fatia A)

Verifier: sub-agent fresco (nao escreveu o codigo). Diff: `develop...HEAD` (b05a9e2). Arvore restaurada apos cada mutacao.
Veredito: **FAIL** (3 criterios com mutacao sobrevivente; comportamento correto, guardas insuficientes). `verify.sh`: exit 0.

## Resumo
| # | Criterio | Resultado |
|---|---|---|
| 1 | Todo caminho de duracao de `Travel` passa por `Mobility.TravelTicks` | PASS (codigo) / FAIL (guarda: 2 de 3 sitios sem teste) |
| 2 | Par base/tratamento, `pace>0` no tick 0, >=18/20 seeds | PASS |
| 3 | Desligar Mobility muda o hash em 10 anos | FAIL (teste nao discrimina) |
| 4 | Sensor de escala <=103% / <=102% | PASS |
| 5 | load/reach FUTURE_DEPENDENCY no audit | PASS |
| 6 | default.json com Emergence carrega, goldens passam | PASS (nota) |
| 7 | Docs <=100 linhas | PASS |

## 1. Caminhos de duracao
`grep -rnE "TicksBetween|TravelTicksCache|TravelResolution\.|Mobility\." src`: sitios de duracao de NPC em `Travel`:
- `BehaviorDecisionSystem.Schedule.cs:27` (`TravelArrivalTick`) e `:164` (`LeadHoursTo`) e `BehaviorDecisionSystem.cs:230`
  (`TryCompleteAction`) chamam `Mobility.TravelTicks`.
- Migracao: `MigrationSystem.cs:114` so faz `SetCurrentAction(Travel)`; a duracao vem de `TryCompleteAction` (:230). OK.
- Restantes sao legitimos: `Schedule.cs:108` e `Effects.cs:236` (trajeto casa-mercado, ja com `Mobility.Apply`),
  `TechDiscoverySystem.cs:135` (pressao, ja com bonus), `MarketIndex.cs:46/68/160` (ordenacao por distancia, sem duracao),
  `LazyPosition`/`MovementRouteKind` (rota cosmetica; `grep ArriveAtEnd src` nao acha uso fora do dominio).
Mutacoes (`bash scripts/test.sh`, suite completa 2460 testes):
- M1 `TravelTicks` devolve `ticks` sem bonus: **2 reprovam** (fracao 5/20; teste de encurtamento). OK.
- M2 `BehaviorDecisionSystem.cs:230` volta a `TicksBetween`: reprova ("so 13/20 seeds"). OK.
- M3 `Schedule.cs:27` volta a `TicksBetween`, isolado: **0 falhas, 2457 passam** -> sobrevive.
- M4 `Schedule.cs:164` (`LeadHoursTo`) volta a `TicksBetween`, isolado: **0 falhas** -> sobrevive.
- M3+M4 juntos: reprova so o teste de fracao (so em conjunto com o sitio de conclusao inalterado).
Consequencia: dois dos tres sitios podem regredir sem nenhum teste reprovar (agendamento de chegada e antecedencia de saida).

## 2. Direcao em 20 seeds
`bash scripts/test.sh --filter MobilityTravelTests`: 3/3 passam (`MobilityTravelTests.cs`, teste de 20 seeds, limite >=18).
Mutacao M1 derruba para 5/20 -> o teste discrimina.

## 3. Hash em 10 anos
Teste: `Turning_mobility_off_changes_the_canonical_hash` (`MobilityTravelTests.cs`). Compara mundo com
`ForceAdoption` vs sem. A adocao ja grava a tecnologia no estado (`WorldSnapshot` serializa `Technologies`), logo o hash
difere mesmo sem consumidor. Em M1 (bonus removido) o teste **continua passando** -> nao prova que Mobility entra na conta.
Correcao sugerida: comparar tratamento com bonus vs tratamento com `PaceBonusPctPerPoint = 0` (mesma tecnologia no estado),
ou comparar hash apos 10 anos de dois tratamentos que diferem so no bonus.

## 4. Sensor de escala
`git worktree add --detach /private/tmp/lw-verify-base <merge-base 6799bf3>`; `bash scripts/compare-simulation-cost.sh`
(3 rodadas): antes 0,806 us/NPC-tick, 258817 B/tick; depois 0,800 us, 258794 B.
cpuRatio 0,9926 (<=1,03), allocationRatio 0,9999 (<=1,02). PASS. (Plano registra 94,1%; medi 99,3%; ambos dentro.)
Worktree removido.

## 5. load/reach
`docs/audits/living-world-cohesion-audit.md:30` marca `load`/`reach` como FUTURE_DEPENDENCY; `:29` marca `pace` CAUSAL. PASS.

## 6. default.json
`scenarios/default.json:156` `"Emergence": { "Enabled": true }`. `bash scripts/verify.sh`: exit 0 ("verify: OK"), 2457 passam,
3 skip (baselines manuais); goldens `tests/golden/world-hashes.json` verdes. Mutacao: remover a linha -> 0 falhas
(nenhum teste exige o bloco ligado no cenario padrao). O criterio literal passa; a decisao do usuario (ligar) nao e
guardada por teste. Observacao, nao FAIL.

## 7. Docs
`bash scripts/check-docs.sh`: OK. `plan.md` 35, audit 50, phase-24 98 linhas.

## FAILs a corrigir
1. Teste do criterio 3 nao discrimina (passa com o bonus removido).
2. Sem teste que prenda `Schedule.cs:27` e `Schedule.cs:164` a `Mobility.TravelTicks` (M3/M4 sobrevivem). Sugestao:
   teste de `TravelArrivalTick`/`LeadHoursTo` com tecnologia adotada (chegada agendada menor que sem tecnologia).
3. Ajustar `plan.md` criterio 4 ("verificado: 5/20") — vale so para M1; M3/M4 nao sao cobertos hoje.
