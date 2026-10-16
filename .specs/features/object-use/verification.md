# Verification — object-use (#47 fatia 2: uso e animação atrelados ao objeto)

Verifier independente (contexto em branco), sobre `402996a` (pós-rebase na `develop`). Veredito: **PASS**.
Entradas: issue #47 (só fatia 2), `plan.md`, ADR-0037, `git diff origin/develop...HEAD`. Relato anterior descartado.

## Gates (rodados por mim)
- `bash scripts/verify.sh` -> `verify: OK` (exit 0; Unit 2363 passed, 3 skipped, 0 failed).
- `bash scripts/test.sh --project integration` -> 443 passed, 0 failed.
- `bash scripts/client-test.sh` -> `client-test: 287 passed, 0 failed` (17 em `test_object_use`).
- `bash scripts/check-docs.sh` -> `check-docs: OK`.

## Critérios
| # | Critério | Veredito | Evidência |
|---|---|---|---|
| 1 | NPC dormindo em cama do catálogo fica no ponto de uso com a pose declarada (headless) | PASS | `test_object_use.gd:68` (posição = base + offset, `rotation.y`, `pose`, `use_pose`, `use_bed=false`); `interior_view.gd` `_use_of` |
| 2 | Objeto sem pose declarada usa a pose da ação | PASS | `test_object_use.gd:86`; `ObjectUseTests.cs:73` (loader aceita ponto sem pose/ação) |
| 3 | Pose declarada vence a da ação; desconhecida cai na da ação | PASS | `test_object_use.gd:59,80`; `animation_catalog.gd` `pose_for(action, use_pose)` |
| 4 | Servidor informa no delta/snapshot qual objeto o NPC usa | PASS | `InteriorProjector.cs` `Build` e `BuildOccupants` (`UsingObject`); `ObjectUseTests.cs:126,199,216` |
| 5 | Trocar só o objeto em uso é mudança de delta | PASS | `InteriorOccupancyDiffer.cs:30`; `ObjectUseTests.cs:228`; `test_object_use.gd:153,234` |
| 6 | Sem catálogo / ação sem peça / tipo de prédio sem a peça: nada muda, campo omitido | PASS | `ObjectUseTests.cs:138,150,162,186`; `test_object_use.gd:94,103,111,201` |
| 7 | Primeira peça da mobília que atende vence; vários NPCs = peças distintas por id, com volta ao início | PASS | `ObjectUseTests.cs:176`; `test_object_use.gd:117,126` |
| 8 | Ação/pose desconhecida recusada com caminho; vocabulário = `POSES` do cliente | PASS | `ObjectUseTests.cs:87,101`; loader `.action`/`.pose` |
| 9 | Outro andar não usa objeto do térreo; leitos de hospedagem do andar de cima não são alvo | PASS | `test_object_use.gd:132,211` (5 leitos no andar 1, 25 alvos no térreo) |
| 10 | Reabrir outro prédio não herda bases/pontos (inclusive outro com cama própria) | PASS | `test_object_use.gd:167,185` |
| 11 | Mobília padrão: cama/mesa/banco/bigorna/balcão com ação, pose, offset, facing | PASS | `DefaultObjects.cs`; `ObjectUseTests.cs:255,268` |
| 12 | Hash do mundo inalterado; `usingObject` fora do estado | PASS | ver Determinismo |

## Mutações (cada uma compilou/parseou; cada uma reprovou ao menos um teste; revertidas via `git checkout --`)
As duas pedidas: remover `if level == 0` do registro de base -> reprova `test_lodging_beds_on_upper_floors...`;
remover `_object_bases.clear()` em `_clear` -> reprova `test_reopening_a_building_that_also_has_a_bed_uses_only_its_own_bed`.
Cliente (18, todas mortas por `test_object_use`): `_register_base` vazio (10 falhas), `_use_of` sem checar andar (2), sem peça colocada (1),
`_use_ranks` sempre 0 (1), pose vazia (4), `_use_point` ignora ação (1), rank ignorado (1); `set_occupants` mantendo cama própria (1), ignorando
posição (5), ignorando facing (1), sem re-aplicar pose quando só `use_pose` muda (4); `animation_catalog` ignorando pose do objeto (3) ou aceitando
pose desconhecida (1); `npc_actor` ignorando/não guardando `use_pose` (2/2); `world_store` descartando `usingObject` do delta (1).
Servidor (23, via `scripts/test.sh --filter` em Objects/Visual.Projection/InteriorOccupancy): loader sem action (14 falhas), sem pose (8), sem validar
action (1), sem validar pose (1); `ObjectPoses.Known` sem `power` (1) / com `levitate` (2); `DefaultObjects` com bed.action, table.pose, bench.offset,
anvil.facing, bed.pose alterados (6/1/1/1/4) e balcão removido (4); `InteriorProjector` sem catálogo em `BuildOccupants` (2) e em `Build` (4),
tipo de prédio fixo em `Build` (3) e em `BuildOccupants` (1), última peça vence (1), ponto sem ação atrai (1), `Build` ou `BuildOccupants` sem uso
(4/2), `JsonIgnore` removido (1), ação ignorada (5); `InteriorOccupancyDiffer` sem comparar `UsingObject` (1).
Sobreviventes: nenhum. `git status` limpo ao fim (só este arquivo modificado).

## Determinismo e regressão
- `git diff origin/develop...HEAD -- src` sem `Random`, `DateTime.Now/UtcNow` ou `Guid.` adicionados; Simulation/Domain só mudaram loader, `DefaultObjects`
  e `ObjectCatalog` (dado de cenário), nada de tick.
- `tests/golden`, `tests/baselines`, `tests/contracts`, `tests/fixtures`: diff vazio contra `origin/develop`; `verify.sh` e integração verdes (hashes inalterados).
- `UsingObject` só existe em `InteriorProjector.cs` e `InteriorOccupancyDiffer.cs` (projeção da API), nunca em `WorldState`/hash.
- `stress:500`, 3 rodadas, mesma carga: cabeça 75,8 / 76,4 / 74,6 (média 75,6); `origin/develop` 76,4 / 73,5 / 75,8 (média 75,2). Acima de 50 FPS e
  coerente com a base (ruído da máquina carregada). Worktree temporário de base removido.

## Menores (sem FAIL)
- `plan.md` cita desempenho do motor (MeasureHarness) que não reproduzi: o tick não foi tocado (diff sem arquivos de tick), então não há risco.
- Testes de integração sobre `usingObject` ponta a ponta HTTP não existem; coberto por teste de unidade do JSON do cliente (`ObjectUseTests.cs:199`).

## Veredito final: PASS
