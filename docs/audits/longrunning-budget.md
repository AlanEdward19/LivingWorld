# LongRunning — orçamento de tempo (2026-10-10)

`bash scripts/test.sh --project longrunning` — 138 testes, 4 núcleos, Release.

| Estado | Relógio | Observação |
|---|---|---|
| Antes de qualquer otimização | > 25 min (não concluía) | `Ten_k` sozinho: 23 min |
| Otimizações sem mudar comportamento (AD-074/075/076) | **6,8 min** | hashes idênticos ao código original |
| + P1 + P2 + P3 | 4,8 min | medido **com a população morrendo de fome** (mundo pequeno = barato) |
| + P1–P3 + B1 + Midshift + realismo (AD-077/078), sem corte novo | 25 min | a população sobrevive, então o custo acompanha os vivos |
| + `One_sim_year` sem 5.000 NPCs (aplicado) | **12 min** (soma 26,7 min de teste) | 111 passam, 0 falham, 7 ignorados |

Maiores restantes: `Ten_k` 346 s, `Population_100x` 289 s, sensor 5k 193 s, `FamilyPaired` Vitality 103 s, `BytesPerNpc` 92 s.

## Corte aplicado depois de B1
| # | Ajuste | Ganho | Impacto |
|---|---|---|---|
| P4 | `One_sim_year…`: remover 5.000 NPCs (ficou só 1.000) | −13 min de teste, 25 → 12 min de relógio | A escala grande segue coberta pelo sensor de 30 dias e por `Ten_k`; perde-se "5k sobrevive a 1 ano" |

## Propostas que mudam o que o teste cobre
Aplicadas: P1, P2, P3 (aprovadas) e P4.
| # | Ajuste | Ganho estimado | Impacto |
|---|---|---|---|
| P5 | `Ten_k`: 30 → 14 dias | ~−3 min de teste | cobre menos dias de escala; custo/dia é plano, a medida por NPC-dia não muda |
| P6 | `Population_100x`: 1 ano → 90 dias | ~−3,5 min | o teto de iterações por tick estoura nos primeiros dias (P1) |
| P7 | Sensor 5k: mês → 14 dias | ~−1,5 min | baseline de escala regravado em 14 dias |
Com P5–P7 o relógio deve ficar em torno de 7–8 min; abaixo de 5 min exigiria também tirar 10k/100x do conjunto padrão e deixá-los em `--scenarios` noturno.

### P1–P3 (aplicadas)
| # | Ajuste | Ganho medido | Impacto |
|---|---|---|---|
| P1 | `Population_100x…tick_iteration_budget`: 10 → 1 ano | −1,5 min | O teto de iterações por tick estoura nos primeiros dias, não em décadas; perde-se só a garantia de "10 anos sem estourar" |
| P2 | `FamilyPaired` (população média e Vitality): 20 → 10 seeds | −0,5 min | IC95 bootstrap fica mais largo (mais difícil provar paridade); a média-base de `tests/baselines` deve ser regravada |
| P3 | `Ten_k` 90 → 30 dias; `Serialized_size` 60 → 30 anos | −0,5 min | Cobre menos horizonte de escala/armazenamento; saturação do snapshot já é visível em 30 anos |

## Flutuação conhecida
`ObservationalLodSensorTests.Approximate_cosmetic_cost…` compara tempos medidos (ns/NPC-tick): isolado dá 0,27 (teto 0,65);
dentro da suíte, com carga, às vezes passa de 0,65. Não é regressão.

## Falhas que já existiam (confirmadas no commit original)
`Birth_count…` e `Every_birth…` contavam `WorldEventKind.Birth` de plantas/animais como nascimentos de NPC — **corrigido** (filtro por `NatalitySystem`; idade na concepção sem `AgeYears`, que congela na morte).

## Propostas de comportamento do motor (B1 e B2 aplicadas; ver AD-077/078 e `realism-cadence.md`)
| # | Achado | Proposta | Impacto no comportamento atual |
|---|---|---|---|
| B1 | `RollDeathAge` parte da idade 0 para todo NPC: um adulto sorteado com idade de morte menor que a atual morre 1 h depois (40–100% do "ano 1") | Ligar `PopulationRules.ConditionalInitialMortality` (já implementada, desligada) no cenário default e no fixture de escala | População inicial sobrevive ao ano 1 (default: 43 → ~80 vivos). Muda **todos** os hashes, goldens e baselines |
| B2 | A comida de plantas é depositada uma só vez por planta (`DepositYield`); produção ≈ 10–40% do consumo em escala; inanição passa a ser a causa dominante depois do 1º ano | Rebalancear produção/replantio (ou reserva) por tamanho de população | Sem isso a vila default fica com 1 NPC viva a partir do ano ~8 e `Ten_k`/`Storage` não medem escala estável. Muda economia e demografia |
| B3 | Cada NPC acorda ~a cada 1,4 h (240 mil acordares/ano por 40 NPCs); é o custo intrínseco a 10 mil NPCs (~100 s por ano simulado) | Acordar por evento/necessidade (não por hora) quando ocioso | Mudaria a ordem das decisões: hashes e comportamento fino mudam; ganho potencial de várias vezes em escala |
