# Verification — object-effects (#47 fatia 3)

Verifier independente, contexto em branco. Branch `feat/objetos-efeitos-fatia3` (HEAD b74f1cf), base `origin/develop` d0061ff. Relato anterior do autor ignorado e sobrescrito.

**Veredito: PASS** (0 FAIL; sobreviventes só menores, abaixo).

## Gates rodados por mim
| Comando | Resultado |
|---|---|
| `bash scripts/verify.sh` | exit 0; Unit 2422 passed / 0 failed / 3 skipped |
| `bash scripts/test.sh --project integration` | 448 passed / 0 failed |
| `bash scripts/client-test.sh` | exit 0 (ruído de RID leak do Godot, pré-existente) |
| `bash scripts/check-docs.sh` | `check-docs: OK` |
| `test.sh --project longrunning --filter GoldenHashes` | 4 passed, 1 skip (gravador); `tests/golden` fora do diff |

## Critérios da issue (fatia 3)
| Critério | Veredito | Evidência própria |
|---|---|---|
| Efeitos `alvo:magnitude` só pelo motor | PASS | `ObjectEffect` em `ObjectCatalog.cs`, parse em `ObjectCatalogScenarioLoader.cs` (ParseEffects), aplicação só em `ObjectUse.cs` (Bonus/DurationHours/ComfortOf). Nenhum `Random`/`DateTime`/`Guid`/`Math.Pow|Exp|Log|Sin|Cos` nos 3 arquivos (grep vazio). Cliente intocado. |
| Objetos no DecisionContext como oportunidade, sem `ActionType` novo (AD-012) | PASS | `DecisionContext.Comfort` (campo novo no fim, default); `BehaviorDecisionSystem.Utility.cs` multiplica só Eat/Sleep. `ActionType` não está no diff. Mutações S1-S7, D1: mortas. |
| Par base/tratamento, cama melhor mais rápido em >= 18/20 seeds | PASS | Teste `A_better_bed_...18_of_20` passa. Sonda minha (20 seeds, 192 ticks, 30 NPCs): `sleep:1.0` > `sleep:0` em 20/20 (média ~81 vs ~63); controle `sleep:0` == sem catálogo em todas as seeds; dose-resposta: `sleep:-0.5` pior em 20/20, `sleep:3` melhor que `1.0` em 20/20. Não passa por acaso (cego a 18/20 por ruído seria p~1e-4; aqui há dose-resposta e controle idêntico). |
| Desligar o sistema muda o hash | PASS | `Turning_the_effects_on_changes_the_world` + `Effects_of_zero_leave_..._as_without_objects` (controle exato). Mutação OFF (Bonus sempre 0): 15 testes falham, incl. 18/20. |
| Conservação de recursos | PASS | `Faster_meals_and_sleep_keep_the_money_and_stock_accounting_closed_every_day` (90 dias, 40 NPCs, efeitos 1.0): dinheiro cunhado/destruído fecha, estoque >= 0. Passa na suíte. |
| Escopo mínimo (sleep/eat, sem estado por instância, sem `skill:`/`stock:`) | PASS | `ObjectEffectTargets.Known = [sleep, eat]`; `ObjectUse` sem estado; alvo desconhecido recusado (L9 morta). |

## Critérios do plan.md
| Critério | Veredito | Evidência |
|---|---|---|
| Carrega, sobrevive ao snapshot; inválidos recusados com caminho | PASS | Mutações L1-L13, C1, C2 mortas (alvo, magnitude <= -1, NaN, soma <= -1, ação repetida/inexistente, ids negativos, `effects`/`interactions` não-lista, chave "03"). |
| Duração `ceil(8/(1+m))` min 1; alvo isolado; fora de casa/sem móvel/sem requisito = sem efeito | PASS | U13/U14/U15/U17/U21a/U21b/U22/U23 mortas. |
| Efeito 0 = mundo sem catálogo; mesma seed = mesmo mundo | PASS | Testes acima; ver determinismo. |
| Agendador e conclusão concordam; apresentações usam a duração curta | PASS | B1, W1, P1-P4 mortas (cada uma reprova teste próprio). |
| Utilidade Sleep/Eat x (1+conforto), sem tocar outras ações | PASS | S1-S7 mortas; `Neither_comfort_leaks_...` cobre Socialize/Buy/Work/Travel/Idle e o outro alvo. |
| `usingObject` respeita `interactions` | PASS | U2, I1, I2 mortas. |
| Sem catálogo / catálogo antigo = mesmo hash | PASS | Ver abaixo. |

## Determinismo e hash contra a base
Worktree temporário em `origin/develop` (removido). Sonda (untracked, removida) em base e HEAD: seeds 7 e 11, 40 NPCs, 720 ticks, 2 variantes (sem `Objects`; catálogo ANTIGO `DefaultObjects` com mobília `-1:[bed,table]` sem effects/interactions): `CanonicalHash` e SHA-256 do `WorldSnapshot.Serialize` (355-363 KB) **idênticos** nas 4 combinações (diff vazio entre `probe-base` e `probe-head`). Goldens do repo passam.
`x * (1.0 + 0f)`: float 0 -> double 0.0 exato; `x*1.0 == x` em IEEE (sem NaN/inf: magnitudes finitas > -1). Mutação "ruído" `1.001 + Comfort` reprova os goldens (sensor real).
`ConditionalWeakTable` do `ObjectCatalog`: chave por referência, índice derivado de dados imutáveis, sem ordem de iteração de hash em decisão (listas ordenadas; `Dictionary` só para lookup). Seguro para thread (GetValue). Sem impacto no JSON (record `Index` privado, fora do snapshot).
Acordar: duração >= 1 sempre (Ceiling sobre base > 0; piso -0.99 em `Bonus` e loader recusa soma <= -1), logo ninguém dorme sem fim; o ganho de sono por ciclo é mantido (sono médio sobe, fração dormindo cai: 0,31 -> 0,26).

## Mutações (todas revertidas; `git status` limpo salvo este arquivo)
~70 mutações válidas executadas via `scripts/test.sh` (filtro Objects|Decision|Interior|Wake|Presentation|Snapshot; goldens para as de hash). Mortas: ObjectUse (Serving ação/Allows, Allows ação/npc null/profissão/skill incl. fronteira `<=`/default/inversões, Bonus mustBeHome/alvos/soma/alvo na soma/piso -1.0, DurationHours Floor/Round/divisor/mustBeHome, ComfortOf mustBeHome por alvo/troca/zero), loader (L1-L4, L5a, L6, L7, L9-L13), `ObjectCatalog` (JsonIgnore x2, tipo próprio, default, Find), Utility (S1-S7), scheduler/decisão/apresentações (B1, W1, P1-P4), Builder (D1), InteriorProjector (I1, I2), OFF, ruído.

## Sobreviventes (menores; nenhum critério declarado fica sem proteção)
- U12 `interactions: []` tratado como "sem interações" (qualquer um usa); mutar para "ninguém usa" não reprova. Semântica não declarada no plano.
- U19b piso -0.99 -> -0.5: nenhum teste usa magnitude entre -0.99 e -0.5; só -1.0 e ausência de piso são pegos. Sugestão: caso `sleep:-0.75` => 32 h.
- U20 `Max(1, ...)`: equivalente (Ceiling de base > 0 já dá >= 1).
- L5b (`min` não finito) e L8 (`cut <= 0` vs `< 0`): equivalentes (JSON não codifica NaN; alvo vazio cai em "desconhecido").
- C5 (`TryAdd` vs sobrescrita): equivalente, loader recusa id duplicado.
- D2 `DecisionContextCache.BuildIncremental` sem `Comfort`: sem chamador em `src` (só testes). Latente: o ramo "nada sujo" devolve `entry.Context with {...}` com Comfort antigo. Se o cache for ligado, precisa teste de paridade com `Build`.
- Observação: `DurationHours` é recalculada a cada chamada; se profissão/habilidade/moradia mudarem no meio da ação o fim muda (agendador e conclusão usam a mesma função, então não divergem entre si). Sem teste, sem critério.

## Desempenho (MeasureHarness, 1000 NPCs, rodadas alternadas base/HEAD, máquina compartilhada)
| | base (µs/NPC-tick; B/tick) | HEAD catálogo desligado |
|---|---|---|
| r1 | 0,829; 256 067 | 0,816; 258 690 |
| r2 | 0,814; 256 057 | 0,826; 258 825 |
| r3 | 0,825; 256 175 | 0,810; 258 725 |
Médias: 0,823 vs 0,817 µs = 99,3% (limite 103%); alocação 256 100 vs 258 747 = 101,0% (limite 102%). Ruído entre rodadas ~2%. Vilas (5 village + 3 live) idênticas à base nas rodadas 1 e 3 (comparado sem `ms=`). Catálogo ligado (`LW_MEASURE_OBJECTS=1`, 1 rodada): 0,826 µs, 240 682 B/tick, snapshot 13 385 B/vivo; vilas diferem por desenho. Desempenho: PASS.
