# Plano lean — Crença de escassez só pesa para quem pode agir (regressão da #91)

Status: implementado; verificação em `verification.md`.

## Problema
`scripts/verify.sh --full` falhava em `Live_world_population_stays_within_90_percent_of_the_baseline` (213 → 136 no ano 5).
Bisect: commit da #91 (história nos mundos do catálogo, ADR-0037). Morte por fome vira crença "hunger" e dava +40 em `Buy`
a qualquer NPC, mesmo sem dinheiro ou com a despensa cheia; ele ia ao mercado em vez de trabalhar e perdia renda.
Desligar o bônus ou o limiar de história restaurava a população (isolado nos dois casos).

## Fluxo
`ContextFactorBonus` só concede o +40 de `Buy` se `CanActOnScarcityBelief`: carteira ≥ 1 e menor estoque entre comida e água
abaixo de `membros × ProvisionTargetDays` (economia desligada ou sem casa: só a carteira conta).

## Critérios (EARS)
1. Com a crença, dinheiro e despensa vazia, `Buy` ganha +40; sem dinheiro ou com despensa cheia, ganha 0
   (`RuntimeHistoryTests.Starvation_creates_oral_history_...`).
2. A crença ainda decide a ação de quem pode comprar (`Scarcity_belief_diverges_decision_toward_Buy`).
3. População do mundo ao vivo no ano 5 ≥ 90% do baseline original, sem regravá-lo.
4. Mutação: remover o gate reprova 1 e 3.

## Portas de mão única
Nenhuma: sem contrato público, migração ou baseline novo. Muda a decisão só de quem tem a crença (mundos com história).

## Fora de escopo
Rebalancear o peso de 40 ou o limiar 0.4 da #91.
