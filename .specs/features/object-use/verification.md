# Verification — object-use (#47 fatia 2)

Verifier independente (contexto em branco, 2º ciclo). Base `feat/objetos-catalogo-fatia1`, HEAD `0cdafb0`.
Entradas: issue #47 (fatia 2), `plan.md`, ADR-0037, `git diff feat/objetos-catalogo-fatia1...HEAD`. Máquina com carga 3-8.

## Gates rodados por mim
| Comando | Resultado |
|---|---|
| `bash scripts/verify.sh` | `verify: OK`, unit 2328 passed / 0 failed / 3 skipped (pré-existentes) |
| `bash scripts/test.sh --project integration` | 431 passed / 0 failed |
| `bash scripts/client-test.sh` | `278 passed, 0 failed` (14 em `test_object_use`) |
| `bash scripts/check-docs.sh` | `check-docs: OK` |
| `scripts/test.sh --project longrunning --filter GoldenHashes` | 4 passed; `tests/golden/world-hashes.json` sem diff vs base |

## Critérios (PASS/FAIL, evidência)
| # | Critério | Veredito | Evidência (arquivo:linha) |
|---|---|---|---|
| 1 | Dormir numa cama do catálogo: ocupante traz `usingObject="bed"` | PASS | `ObjectUseTests.cs:126` (cama/mesa), `:185` (JSON `usingObject`), `:202` (`BuildOccupants`, fonte do delta) |
| 2 | Cliente põe o NPC no ponto de uso com a pose declarada (headless) | PASS | `test_object_use.gd:68` (posição = base + offset, facing 90°, `pose=="sleep"`, `use_bed==false`) |
| 3 | Objeto sem pose declarada usa a pose da ação | PASS | `ObjectUseTests.cs:73`; `test_object_use.gd:86`, `:59` (`pose_for(SLEEP,"")`) |
| 4 | Pose declarada vence a da ação; pose desconhecida no cliente cai na da ação | PASS | `test_object_use.gd:59`, `:80` |
| 5 | Sem peça que atenda/sem catálogo/sem `usingObject`: nada muda | PASS | `ObjectUseTests.cs:138,150,172`; `test_object_use.gd:94,103,111` |
| 6 | Primeira peça da mobília que atende a ação; vários NPCs em peças diferentes (fila por id, volta ao início) | PASS | `ObjectUseTests.cs:162`; `test_object_use.gd:117,126` |
| 7 | Ação/pose desconhecida recusada com caminho; vocabulário = `POSES` do cliente | PASS | `ObjectUseTests.cs:87,101` |
| 8 | Trocar só o objeto em uso é mudança de delta (servidor e cliente) | PASS | `ObjectUseTests.cs:214`; `test_object_use.gd:139,153,195` |
| 9 | Outro andar não usa objeto do térreo | PASS | `test_object_use.gd:132` |
| 10 | Reabrir outro prédio não herda objetos/pontos (lacuna do ciclo anterior) | PASS | `test_object_use.gd:167` (mutações V16/V17) |
| 11 | Uso sem peça colocada cai no caminho antigo sem erro (lacuna anterior) | PASS | `test_object_use.gd:185` (mutações V10) |
| 12 | "O servidor informa no delta qual objeto o NPC usa" | PASS | `InteriorOccupancyDiffer.cs:30`, `InteriorProjector.cs:43` (`BuildOccupants`); `ObjectUseTests.cs:202,214`; `test_object_use.gd:195` (store) |
| 13 | Hash de mundo sem Objects idêntico à base | PASS | goldens (default, seeds 42/43, 100/3650/17280 ticks) passam e o JSON não mudou no diff; `UsingObject` só existe em `LivingWorld.Api` (grep), logo não entra em `WorldSnapshot`; round-trip com Objects mantém o hash (`ObjectUseTests.cs:113`) |
| 14 | Sem `Random`/`DateTime`/`Guid` novos em Simulation/Domain | PASS | `git diff base...HEAD -- src/LivingWorld.Simulation src/LivingWorld.Domain` filtrado por `Random\|DateTime\|Guid` nas linhas `+`: vazio |
| 15 | Desempenho: cliente `stress:500` | PASS | 3 rodadas por mim: 76,5 / 76,1 / 76,3 FPS (base registrada 75,9-77,2; piso 50) |

## Mutações (todas compilam/parseiam; revertidas com `git checkout -- <arquivo>`; `git status` limpo)
Servidor (filtro unit `ObjectUse|ObjectCatalog|Interior`; REPROVA = pelo menos um teste falhou):
- Loader: descartar `action` (13 falham), descartar `pose` (8), aceitar pose desconhecida (1), não validar `Enum.IsDefined` (1), aceitar pose não-string (1): todas REPROVAM.
- `ObjectPoses.Known` sem `power` (1 falha) e com `levitate` (2): REPROVAM.
- `DefaultObjects`, cada uma das 5 peças: facing do bed, ação da table, pose do bench, offset do anvil, nome do counter: todas REPROVAM (Theory por peça).
- `InteriorProjector`: última peça vence (REPROVA), `Build` ignora tipo de prédio (REPROVA), sem catálogo atribui "bed" (REPROVA), `Build` sem `usingObject` (4 falham), `BuildOccupants` sem `usingObject` (REPROVA), ponto sem ação atrai (REPROVA), ação nula usa algo (REPROVA).
- JSON nulo emitido (sem `WhenWritingNull`): REPROVA. `InteriorOccupancyDiffer` ignorando `UsingObject`: REPROVA.
Cliente (`client-test.sh`):
- `pose_for`: ignorar pose do objeto (3), aceitar pose desconhecida (1), aceitar vazia (vários): REPROVAM.
- `npc_actor.set_action` sem repassar pose (2) e sem guardar `use_pose` (2): REPROVAM.
- `interior_view`: posição ignora ponto (3), facing não aplicado (1), `enable_bed` sempre true (1), fila sem ordenar por id (1), andar≠0 usa objeto (1), ponto de outra ação atrai (1), delta só compara ação (4), sem wrap/módulo (1), sempre 1ª peça (1), offset ignorado (3), pose do ponto ignorada (4), ponto inexistente sem tratamento (1), uso sem peça sem tratamento (1), `_clear` sem limpar os dois mapas (1), bases guardando só a última peça (1): todas REPROVAM.
- `world_store` descartando `usingObject` do delta: REPROVA (1).

## Sobreviventes (menores, nenhum critério declarado fica sem proteção)
1. `InteriorProjector.BuildOccupants` usando tipo de prédio fixo (3) em vez de `building.BuildingTypeId`: nenhum teste reprova (o único teste do delta usa casa, tipo 3). O caminho `Build` e o helper estão protegidos (`:150`); só o argumento no 2º ponto de chamada não. Efeito visual nulo hoje (o cliente ignora objeto sem peça colocada), mas snapshot e delta poderiam divergir. Sugestão: espelhar `:150` para `BuildOccupants` numa loja.
2. `_clear` sem `_object_bases.clear()` sozinho (ou sem `_use_points.clear()` sozinho) não reprova: os dois mapas se protegem entre si no teste de reabrir; remover os dois reprova (V16). Defesa redundante, caso de borda raro (catálogo novo com a mesma id e sem peça colocada).

## Não verificado
- Interior real contra API de dev: subi uma API nova (`LW_DB` próprio, porta 5299, `/worlds/start vila-pequena`), mas `/visual/subscribe?scope=Interior` do prédio 1 voltou sem `objects` e sem ocupantes, então não vi a cama na tela. NÃO verificado ponta a ponta; a cobertura vem dos testes headless. API encerrada, DB removido.
- Desempenho do motor (µs/NPC-tick): não remedido; o diff não toca o tick (só projeção de interior observado). O número do plan.md é do autor.
- Hash "COM Objects" base vs HEAD: não comparado entre commits (os pontos de uso entram no catálogo, que é dado de cenário); verificado só que `usingObject` não é estado (item 13).

## Veredito final: PASS
Todos os critérios da fatia 2 têm teste que reprova sob mutação; as duas lacunas do ciclo anterior (reabrir prédio, uso sem peça) estão fechadas. Dois sobreviventes menores acima.
