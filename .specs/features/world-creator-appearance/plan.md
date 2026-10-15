# world-creator-appearance — #46 fatia 2 (aparência e habilidades da pessoa)

Status: PASS (Verifier rodada 2, `verification.md`); a rodada 1 apontou interior sem teste, corrigido. ADR-0032. Branch `feat/world-creator`.

## Problema
Aparência era só do cliente (sorteio por `npc_id`); filhos não herdavam; o autor não escolhia aparência nem habilidades iniciais.

## Fluxo
Bloco `Appearance` no período (opt-in) → fundadores sorteiam 4 genes `[0,100]` por stream `appearance-{id}`; nascidos herdam
(`Appearance.Inherit`, modelo da personalidade) → `GET /npcs/{id}` e projeções de mundo/cidade/interior expõem `appearance` →
`npc_actor.gd` veste o boneco (rampa de pele/cabelo, volume do cabelo, porte). `AuthoredHouseholds[].Members[]` aceita
`Appearance` e `Skills`; editor "Criar pessoa" ganha genes, habilidades e prévia 3D. Cortejo lê `AttractionFactor.Appearance`.

## Critérios (EARS)
- SEM o bloco, ENTÃO o hash canônico, os golden hashes e os contratos de rota ficam iguais (`Default_scenario_ninety_days_keeps_the_recorded_hashes`,
  `Without_the_rules_nobody_has_looks_and_the_snapshot_has_no_trace_of_them`, `Without_the_period_block_the_field_is_absent_everywhere`).
- MESMA seed + MESMO bloco ENTÃO mesmo mundo, em 2 processos (`Same_seed_inherits_the_same_looks_across_two_separate_processes`).
- Filhos mais parecidos com os pais que com estranhos, z ≥ 3 em 200 pares, com controle h=0 (`Children_look_more_like_their_parents...`).
- LIGAR o bloco muda o hash (`Turning_looks_on_changes_the_canonical_hash...`); o cortejo prefere semelhantes (`Attraction_rewards_similar_looks...`).
- Aparência/habilidade autorada é aplicada exatamente; inválida → erro com o campo; `Appearance` sem o bloco → recusada.
- Cliente: genes do servidor chegam ao ator; sem eles, sorteio de antes; materiais compartilhados (passo de 5); cenário gerado leva `Appearance`/`Skills`.

## Desempenho (registrado)
- Cliente `stress:500` (3 rodadas): sorteio local 76,6/74,0/77,4; com genes 75,0/75,6/75,2 FPS (`client/godot/README.md`).
- Motor, sem o bloco (`ScenarioRunner.Create` com 1000 NPCs, 720 ticks, rodada estável; o Verifier mediu `ScaleScenarioFixture`: 0,776 → 0,774 µs e 255 381 → 255 377 B/tick; antes `0d0a056` → depois): 0,497 → 0,497 µs/NPC-tick (100%) e 172 295 → 172 350 B/tick (100,03%), dentro de 103%/102%; `ScaleScenarioSensorTests` passa. Hash idêntico em 2 processos.
- Interior: `InteriorAppearanceTests` cobre snapshot e delta de interior (com e sem o bloco).

## Portas de mão única
Rotas ganham `appearance` (omitido sem o bloco); formato do cenário ganha `Appearance`/`Skills`; `AttractionFactor.Appearance` no fim do enum.

## Fora de escopo
Roupa/acessórios, aparência no pool agregado, peso de atração sem pesos declarados no cenário (docs/domain/period-authoring-appearance.md).
