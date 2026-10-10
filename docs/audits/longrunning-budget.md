# LongRunning — orçamento de tempo (2026-10-10)

`bash scripts/test.sh --project longrunning` — 138 testes, 4 núcleos, Release.

| Estado | Relógio | Observação |
|---|---|---|
| Antes de qualquer otimização | > 25 min (não concluía) | `Ten_k` sozinho: 23 min |
| Otimizações sem mudar comportamento (AD-074/075/076) | **6,8 min** | hashes idênticos ao código original |
| + P1 + P2 (abaixo) | 5,3 min | medido |
| + P1 + P2 + P3 (abaixo) | **4,8 min** | medido |

## Propostas que mudam o que o teste cobre (não aplicadas)
| # | Ajuste | Ganho medido | Impacto |
|---|---|---|---|
| P1 | `Population_100x…tick_iteration_budget`: 10 → 1 ano | −1,5 min | O teto de iterações por tick estoura nos primeiros dias, não em décadas; perde-se só a garantia de "10 anos sem estourar" |
| P2 | `FamilyPaired` (população média e Vitality): 20 → 10 seeds | −0,5 min | IC95 bootstrap fica mais largo (mais difícil provar paridade); a média-base de `tests/baselines` deve ser regravada |
| P3 | `Ten_k` 90 → 30 dias; `Serialized_size` 60 → 30 anos | −0,5 min | Cobre menos horizonte de escala/armazenamento; saturação do snapshot já é visível em 30 anos |

## Flutuação conhecida
`ObservationalLodSensorTests.Approximate_cosmetic_cost…` compara tempos medidos (ns/NPC-tick): isolado dá 0,27 (teto 0,65);
dentro da suíte, com carga, às vezes passa de 0,65. Não é regressão.

## Falhas que já existiam (confirmadas no commit original)
`Birth_count…` e `Every_birth…` contam `WorldEventKind.Birth` de plantas/animais como nascimentos de NPC.

## Propostas de comportamento do motor (não aplicadas)
| # | Achado | Proposta | Impacto no comportamento atual |
|---|---|---|---|
| B1 | `RollDeathAge` parte da idade 0 para todo NPC: um adulto sorteado com idade de morte menor que a atual morre 1 h depois (40–100% do "ano 1") | Ligar `PopulationRules.ConditionalInitialMortality` (já implementada, desligada) no cenário default e no fixture de escala | População inicial sobrevive ao ano 1 (default: 43 → ~80 vivos). Muda **todos** os hashes, goldens e baselines |
| B2 | A comida de plantas é depositada uma só vez por planta (`DepositYield`); produção ≈ 10–40% do consumo em escala; inanição passa a ser a causa dominante depois do 1º ano | Rebalancear produção/replantio (ou reserva) por tamanho de população | Sem isso a vila default fica com 1 NPC viva a partir do ano ~8 e `Ten_k`/`Storage` não medem escala estável. Muda economia e demografia |
| B3 | Cada NPC acorda ~a cada 1,4 h (240 mil acordares/ano por 40 NPCs); é o custo intrínseco a 10 mil NPCs (~100 s por ano simulado) | Acordar por evento/necessidade (não por hora) quando ocioso | Mudaria a ordem das decisões: hashes e comportamento fino mudam; ganho potencial de várias vezes em escala |
