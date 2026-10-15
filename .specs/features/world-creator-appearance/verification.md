# Verification — world-creator-appearance (#46 fatia 2, HEAD cd74d8b, rodada 2)

Verifier independente (contexto em branco), 2026-10-15. Veredito: **PASS** (todos os critérios, 6/6 mutações reprovadas).
A falha da rodada 1 (interior sem teste) está corrigida: `InteriorAppearanceTests.cs` reprova a mutação do interior.

## Gates (rodados por mim)
- `bash scripts/verify.sh` -> `verify: OK` (Unit 2265 passed, 3 skipped = gravadores de baseline).
- `bash scripts/test.sh --project integration` -> 420 passed, 0 failed.
- `bash scripts/client-test.sh` -> rc 0, `246 passed, 0 failed`.
- `bash scripts/check-docs.sh` -> `check-docs: OK`.
- `git diff 0d0a056..HEAD -- tests/golden tests/contracts` -> 0 linhas (também `tests/baselines`).

## Critérios
| # | Critério | Veredito | Evidência |
|---|---|---|---|
| a | Sem o bloco: hash/golden/contratos iguais | PASS | Goldens/contratos/baselines intactos no diff; `OptimizationEquivalenceTests.Default_scenario_ninety_days_keeps_the_recorded_hashes` verde no verify; `AppearanceTests.cs:131` (sem regras: ninguém tem genes, snapshot sem rastro); API real (período padrão): `curl /npcs/1 \| grep -c -i appearance` = 0. Mutação (v) reprova. |
| b | Mesma seed + bloco = mesmo mundo, 2 processos | PASS | `test.sh --project longrunning --filter DeterminismTwoProcessTests` -> 10 passed (inclui `Same_seed_inherits_the_same_looks_across_two_separate_processes`, `DeterminismTwoProcessTests.cs:111`). Também `AppearanceTests.cs:140`. |
| c | Filhos mais parecidos com pais, z>=3, 200 pares, controle h=0 | PASS | `AppearanceTests.cs:56` (`pairs = 200`, `Assert.True(z >= 3)`), controle h=0 em `:76` e `:35`; mutação (i) reprova `:56`. |
| d | Autoria exata; recusas com o campo; Appearance sem bloco recusada | PASS | `AppearanceTests.cs:210` (exata), `:227` (sem bloco, nomeia o campo), `:241` (Theory inválidos), `:200` (bloco validado). |
| e | `appearance` em /npcs/{id}, cidade, mundo e interior; omitido sem bloco | PASS | `AppearanceEndpointTests.cs:37,51,64` (npcs, cidade, mundo; ausente em todos sem o bloco); interior (snapshot e delta, com e sem bloco): `InteriorAppearanceTests.cs` 2 testes. Mutação (ii) reprova. |
| f | Cliente | PASS | `client-test.sh` 246 ok (`test_appearance`: genes->ator, sem genes mantém sorteio, materiais compartilhados, cenário com `Appearance`/`Skills`). Mutação (vi) reprova. |
| g | Desempenho | PASS (ressalva 1) | Ver abaixo. |
| - | Cortejo prefere semelhantes; ligar o bloco muda o hash; nascidos herdam | PASS | `AppearanceTests.cs:97`, `:161`, `:169`; mutações (iii) e (iv) reprovam. |

## Mutações (todas restauradas com `git checkout --`; `git status` limpo depois)
| Mutação | Resultado |
|---|---|
| (i) `Appearance.Inherit` ignora os pais (`return rolled` sempre) | Unit: 1 falha, `Children_look_more_like_their_parents...z_of_at_least_three` |
| (ii) `InteriorProjector.cs`: `n.Appearance` -> `null` nas duas construções | Unit: 1 falha, `InteriorAppearanceTests.Snapshot_and_delta_occupants_carry_the_looks_of_the_npc` |
| (iii) `NatalitySystem` sem herança (`appearance = null`) | Unit: 1 falha, `Newborns_inherit_looks_from_their_parents_when_the_rules_are_on` |
| (iv) `CourtshipSystem` sem o `continue` do fator ausente | Unit: 5 falhas (4 `CourtshipSystemTests` + `Attraction_rewards_similar_looks...`) |
| (v) `IncrementalHasher` sem `OmitsNull` | Unit: 2 falhas, `Default_scenario_ninety_days_keeps_the_recorded_hashes` seeds 1 e 2 |
| (vi) `npc_actor.gd` `apply_look` sempre cedo (`if true:`) | client-test: 245 ok, 1 FAIL `test_actor_wears_the_genes_and_the_extremes_differ` |

## Desempenho (g)
Motor, sem o bloco: teste temporário (removido) rodando `ScaleScenarioFixture.CreateWorld(42, 1000)` por 720 ticks, 5 rodadas
no mesmo processo, em worktree de `0d0a056` contra HEAD. Rodada 0 é aquecimento; rodadas 1-4 (estáveis):

| | µs/NPC-tick (r1..r4) | média | B/tick (r1..r4) | média |
|---|---|---|---|---|
| antes `0d0a056` | 0,837 / 0,771 / 0,756 / 0,740 | 0,776 | 255136 / 255239 / 255590 / 255561 | 255381 |
| depois HEAD | 0,828 / 0,772 / 0,744 / 0,753 | 0,774 | 255071 / 255281 / 255592 / 255564 | 255377 |

Razão: tempo 99,7% (limite 103%), alocação 100,0% (limite 102%). Hash idêntico em 2 processos (critério b).

Cliente `stress:500` vs `stress_look:500`, API local em :5214, 3 rodadas alternadas, FPS:
`stress` 76,7 / 78,2 / 76,3 (média 77,1); `stress_look` 75,6 / 75,8 / 74,1 (média 75,2). Diferença -1,9 FPS, dentro de ±3.

## Ressalvas (não bloqueantes)
1. Os números absolutos do motor no `plan.md` (0,497 µs, 172 295 B/tick) vêm de outro cenário/fixture e não reproduzem aqui
   (fixture `ScaleScenarioFixture`: 0,77 µs, 255 mil B). A razão antes/depois, que é o critério, bate (~100%) nas duas medições.
2. Medição do motor feita em uma só máquina, sem controle de carga; ruído de ~±5% entre rodadas, razão bem abaixo dos tetos.
3. API (:5214) encerrada, `/tmp/lw-verif2.db` e worktree temporário removidos; árvore limpa exceto este arquivo.
