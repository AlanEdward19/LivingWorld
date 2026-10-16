# Plano lean — Mobilidade em todo deslocamento (#61 fatia A)

Status: implementado; verificação em `verification.md`.

## Problema
`Mobility` só moldava a despensa e o tamanho da compra (`ProvisionTargetAt`, `PurchaseDaysFor`). O tempo real de todo
`ActionType.Travel` (`TravelArrivalTick`, `TryCompleteAction`, `LeadHoursTo`) usava só custo do mapa × corpo: tecnologia de
mobilidade adotada não encurtava trabalho, cama, mercado, visita nem migração (a migração é `Travel` ao destino de realocação).

## Fluxo
Ponto único `Mobility.TravelTicks(world, npc, origem, destino, multiplicadorCorpo)`: `TicksBetween` × bônus `pace` da
cidade do NPC (`Mobility.BonusPct`). Local distinto custa ≥ 1 tick; mesmo local 0. Os três sítios de duração chamam só ele.
`MarketIndex` só ordena mercados por distância (sem duração): fora do escopo.

## Critérios (EARS)
1. Com tecnologia `pace>0` adotada na cidade, `TravelTicks` de trajeto longo é menor que sem ela; adjacente ≥ 1; mesmo local 0.
2. Mesma seed, base vs tratamento (tecnologia `[7,0,0]` adotada no tick 0), 30 dias: fração de NPC-horas em `Travel`
   menor no tratamento em ≥ 18/20 seeds (direção, não magnitude).
3. Adoção força mudança do hash canônico em 10 anos (entra na conta).
4. Mutação: remover o bônus em `TravelTicks` reprova 1 e 2 (verificado: 5/20 seeds).
5. Sensor de escala ≤ 103% µs/NPC-tick e ≤ 102% alocação/tick.

## Eixos load e reach
Sem consumidor: `FUTURE_DEPENDENCY` no audit (`docs/audits/living-world-cohesion-audit.md`).

## Portas de mão única
`scenarios/default.json` ganha `"Emergence": { "Enabled": true }` (decisão do usuário): o mundo ao vivo passa a emergir
tecnologia. Goldens (`tests/golden`) e pins de 90 dias seguiram idênticos: o bônus é 0 sem tecnologia adotada e nenhuma
emerge nesse horizonte. Hash do mundo ao vivo muda quando a primeira tecnologia emergir.

## Fora de escopo
Fatias B–I da #61; consumidor de `load`/`reach`.

## Desempenho
`compare-simulation-cost.sh`, 3 rodadas antes/depois: 0,807 → 0,759 µs/NPC-tick (94,1%); alocação 258740 → 258772 B/tick (100,01%). Dentro de 103%/102%. Cliente Godot não tocado.
