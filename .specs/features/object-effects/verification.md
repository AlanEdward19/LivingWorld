# Verification — object-effects (#47 fatia 3)

Verifier independente, contexto em branco. HEAD `783377e`, base de comparação = merge-base `2eb1500` (a develop andou: `d0061ff`).
Veredito: **FAIL** (mutantes do núcleo de duração sobrevivem; o desenho em si está correto).

## Gates rodados por mim
| Comando | Resultado |
|---|---|
| `bash scripts/verify.sh` | exit 0 (Unit 2401 ok / 3 skip) |
| `test.sh --project integration` | 443/443 |
| `check-docs.sh` | OK |
| `client-test.sh` | exit 0 (cliente não tocado no diff; ruído de leak do Godot no teardown) |
| `test.sh --project longrunning --filter GoldenHashes` | 4 ok, 1 skip |

## Critérios
| Critério | Veredito | Evidência |
|---|---|---|
| Efeitos `alvo:magnitude` carregam, sobrevivem ao snapshot, inválidos recusados com caminho | PASS (lacunas menores) | `ObjectEffectsTests` (token/interactions/round trip); mutantes L1,L2,L7,L8,L9 mortos |
| Duração = `ceil(8/(1+m))`, alvo cruzado, fora de casa, sem móvel, requisito | PASS | M7,M8,M9,M12,M15,K2 mortos |
| Cama melhor ≥ 18/20 seeds | PASS com ressalva | passa; mas ver F3 (a prova não isola a duração) |
| Desligar muda o hash | PASS com ressalva | ver seção "Prova do hash" |
| Conservação de recursos | PASS (estrutural) | `ObjectUse` não toca estoque/dinheiro; teste só cobre dinheiro e `q >= 0` (vazio pela R1) |
| Agendador e conclusão concordam | **FAIL** | F1, F2 |
| Utilidade Sleep/Eat × (1+conforto), AD-012 (sem ActionType novo) | PASS (parcial) | U1,U2,B1 mortos; sem ActionType novo no diff; F4 |
| `usingObject` respeita `interactions` | PASS | I1,M1,M4 mortos |
| Sem catálogo / sem effects: nada muda | PASS | provas abaixo |

## Questionamentos do desenho
- (a) Agendador x conclusão: ambos chamam `ObjectUse.DurationHours` (`NpcWakeScheduler.cs:76`, `BehaviorDecisionSystem.cs:239`); o scheduler recalcula a cada acordar, então mudança de estado no meio não cria NPC que nunca acorda. Prova empírica (sonda temporária, 5 configs x 3 seeds x 40 dias, 60 NPCs): `overDuration=0` (nenhum Sleep/Eat passa do fim) com efeito ligado. Acordar cedo é barrado por `TryCompleteAction`.
- (b) Determinismo: sem Random/DateTime/Guid/libm em `ObjectUse`/`ObjectCatalog` (grep vazio). `ConditionalWeakTable` usa identidade de referência, fica fora do record (igualdade/hash/JSON intactos; provado abaixo). Soma de efeitos em ordem de lista. Aresta: chaves "03" e "3" de `Furnishings` agora colidem em `ByType` (antes "03" nunca casava); sem efeito no hash. Menor.
- (c) Hash: sonda (seeds 7 e 11, 40 NPCs, 30 dias, com e sem catálogo antigo) em worktree do merge-base e no HEAD: `h0`, hash final, SHA256 do JSON e tamanho **idênticos nos 4 casos**; JSON sem `effects`/`interactions`. PASS.
- (d) `x * (1.0 + 0f)`: tipos todos `double`; 1.0+0.0=1.0 e x*1.0 é exato em IEEE. Suite e goldens inalterados. PASS.

## Prova do hash ("desligar muda")
`Effects_of_zero...` compara digest dos NPCs (efeito 0 == sem catálogo), `Turning_the_effects_on...` exige digest diferente. Está correto em desenho (o catálogo muda o hash canônico, por isso digest). Mas **passa por um mecanismo só**: com a duração desligada (mutante H1: `DurationHours` sempre = base) `Turning_on` e `A_better_bed` seguem verdes, pois o conforto na utilidade basta para mudar o mundo e subir o sono médio. Ou seja, o teste não prova que a duração entra na conta, e o ≥18/20 mede sono médio (proxy), não "recupera mais rápido".

## Mutações (arquivo; teste que reprovou)
Mortas: M1 Serving sem Allows; M2 ordem invertida; M3 default de Allows; M4 profissão invertida; M5 skill `<=`; M7 mustBeHome; M8 alvos trocados; M9 alvo cruzado; M12 Floor; M13 Comfort trocado; M15 fórmula; U1/U2 multiplicadores; B1 Builder sem Comfort; I1 projector sem npc; K2 índice por tipo; K3 "default"; L1 ação inexistente; L2 repetida; L7 alvo; L8 limite -1; L9 NaN; H1.
**Sobreviventes que afetam critério declarado (FAIL):**
- **F1 S1** `NpcWakeScheduler.cs:76` usando `catalog.DurationOf` (duração base): unit inteiro verde. Sonda: efeito `1.0`, `overDuration` 2743–3288 (acorda tarde). O teste `The_wake_scheduler...` é vacuo (`wake <=` passa porque outros candidatos de wake vencem o `min`).
- **F2 S2** `BehaviorDecisionSystem.cs:239` com duração base: unit verde. Sonda: `overDuration` 4926–5302, sono NPC-h 15762 -> 19487 (efeito praticamente anulado). Nenhum teste de mundo detecta.
- **F3** H1 (acima): sem teste que isole o efeito de duração do de conforto. Corrigir: braço só-duração (Comfort neutralizado) com digest diferente e sono médio maior.
- **F4 M14** `ComfortOf` Sleep com `mustBeHome:true` (conforto zera fora de casa): unit completo verde; o teste de conforto só usa NPC em casa. Adicionar teste com NPC fora de casa.
- **F5 P1/P2** `RestPresentation`/`FoodPresentation` com duração base: verde. ADR/plan dizem que usam a mesma função; horas restantes mostradas erram.
Sobreviventes menores: M6 (`npc is null` retorna false; nenhum chamador passa null); M10 (soma vs. atribuição de 2 efeitos do mesmo alvo); M11 e K1 equivalentes (`Max(1,..)` inalcançável pois ceil de positivo >= 1; ids duplicados já recusados); L3/L4/L5 (negativos de profession/skill.id/skill.min sem teste real: `["min"]=-1` int falha `TryGetValue<double>` por tipo, não pela regra); L10 (`effects` não-lista); L6 equivalente; C1 (cache incremental sem Comfort: `BuildIncremental` não é usado pelo tick, só `Build`).

## Desempenho (MeasureHarness, 1000 NPCs, 3 rodadas alternadas, load ~4-5)
| Config | µs/NPC-tick | B/tick |
|---|---|---|
| base (merge-base) | 0,817 / 0,823 / 0,819 | 256 077–256 148 |
| HEAD, catálogo desligado | 0,816 / 0,814 / 0,812 (99,4–99,9%) | 258 708–258 764 (101,0–101,1%) |
| HEAD, catálogo ligado (`LW_MEASURE_OBJECTS=1`) | 0,814 / 0,842 / 0,821 | 240 608–240 714 |
Limites (103% / 102%) cumpridos; ruído de tempo ~±1% na base, ~±3% no ligado. Vilas e mundos vivos: trajetórias idênticas base x HEAD (3 rodadas, `diff` vazio).

## Conclusão
FAIL por F1, F2 (e F3–F5). Correção esperada: testes de mundo que apliquem `overDuration == 0` por hora e comparem horas de sono/refeição com efeito; braço só-duração; comfort fora de casa; presentations. O código atual funciona (sonda), faltam os testes que o prendam. Um novo Verifier deve reavaliar.
