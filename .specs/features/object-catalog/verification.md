# Verificação independente — #47 fatia 1 (object-catalog)

Verifier novo, contexto em branco, branch `feat/objetos-catalogo-fatia1` @ 5a07647. Entradas: issue #47 (fatia 1), `plan.md`, ADR-0034, `git diff origin/develop...HEAD`.
Substitui o verification.md anterior (FAIL), que não foi usado como evidência.

**Veredito final: PASS** (nenhum critério declarado ficou sem proteção; sobreviventes são menores).

## Gates rodados por mim
| Comando | Resultado |
|---|---|
| `bash scripts/verify.sh` | exit 0, `verify: OK` (Unit 2308 passed, 3 skipped) |
| `bash scripts/test.sh --project integration` | exit 0, 431 passed |
| `bash scripts/client-test.sh` | exit 0, `263 passed, 0 failed` (11 testes de `test_object_catalog` ok) |
| `bash scripts/check-docs.sh` | `check-docs: OK` |
| `GoldenHashesTests` (longrunning, modo all, 4 hashes inclusive 3650 ticks) | HEAD 4 passed; worktree temporário em `origin/develop` 4 passed; `tests/golden/world-hashes.json` idêntico (`cmp`) e fora do diff |
| Determinismo | `git diff origin/develop...HEAD -- src` sem `new Random`/`DateTime.Now`/`UtcNow`/`Guid.NewGuid`/`Random.Shared` (grep rc=1); variação é hash inteiro puro (`ObjectCatalog.cs` `ObjectVariation.Index`) |
| `stress:500` (3 rodadas, esta máquina) | 77,2 / 76,0 / 76,3 FPS (base do README 75,9-77,2): acima de 50 e dentro de ~3 da base |

Worktree temporário `vf-develop` removido (`git worktree list` sem ele).

## Critérios (PASS/FAIL, evidência)
| # | Critério | Veredito | Evidência (teste + mutação que reprova) |
|---|---|---|---|
| 1 | `Objects` válido carrega; mobília por tipo de prédio sai do catálogo | PASS | `The_default_objects_load...`, `..._match_the_assets_json_pieces_and_furnishings_exactly`; D1 (sem fallback default), O1/O2/O3/O6 (tamanho, offset y/2, mobília do tipo 3, cor) reprovam |
| 2 | Forma incoerente recusada com caminho: dimensão ≤ 0, parte desconhecida, versão, id repetido, mobília órfã, forma vazia, id/categoria vazios, cor, simetria | PASS | L1 (`<=0`), L2 (finito), L5 (sem capsule), L6 (versão), L7 (id duplicado), L8 (órfã), L9 (chave Furnishings), L10 (regex de cor), L18 (shape vazio), L13/L12 (categoria), L4b (simetria inválida), L19 (bloco órfão) reprovam |
| 3 | Dimensões por forma (box 3, cylinder 2, sphere 1, capsule 2) | PASS | `Each_part_kind_takes_exactly_its_own_number_of_dimensions`: L3, L3b, L3c, L3d reprovam |
| 4 | Campos lidos do cenário: simetria (incl. `xz`), categoria, cor minúscula, seed, use points (offset/facing) | PASS | L4 (sem xz), L14 (simetria perdida), L11 (lowercase), L15 (seed), L17 (facing), L20 (offset de use point) reprovam |
| 5 | Sem `Objects` = nada muda: `null`, campo omitido no snapshot, hash igual | PASS | L21 (catálogo vazio no lugar de null), W1/W1b (sem `[Canonical]`/sem JsonIgnore), P3/P6 (interior sem `objects`) reprovam; Golden idêntico develop x HEAD |
| 6 | Com `Objects`: catálogo sobrevive ao snapshot e entra no hash | PASS | W1, W2 (snapshot não relê catálogo) reprovam `..._round_trip_with_the_same_canonical_hash` e `WorldSnapshotTests.Mutating_a_canonical_field...` |
| 7 | Cenário -> mundo: validador e loader propagam o catálogo; seeder dos presets o inclui; bloco inválido invalida o período | PASS | W3, W4, W5, S1 reprovam `DefaultPeriodSeederTests` (Integration) |
| 8 | Variação determinística por seed e posição; seed 0 nunca varia; seed por peça (não a da primeira) | PASS | D2, D4, D5, P1 (slot fixo), P2 (seed da 1a peça) reprovam |
| 9 | Snapshot de interior com as chaves que o cliente lê, mobília do tipo do prédio | PASS | P4 (tipo errado), P5 (catálogo vazio) reprovam `The_interior_snapshot_json_has_the_keys_the_client_reads` |
| 10 | Cliente: interior do catálogo = interior de hoje (contagem, posição, tamanho, cor) | PASS | C2, C10, C17, C21, V5 reprovam `test_interior_from_the_catalog_matches_the_assets_json_interior` |
| 11 | Cliente: vista desenha o catálogo do servidor e cai no `assets.json` sem `objects`/id ausente | PASS | V1, V2 (variação), V3 (fallback), V7 reprovam; T1 (`world_store` perde `objects`) reprova `test_the_store_keeps_the_objects...` |
| 12 | Mesh/material compartilhados por (id, variação); 50 objetos = 1 mesh por par | PASS | C1, C2, C4, C5 reprovam `..._share_mesh_and_material` e `test_fifty_objects...` |
| 13 | Primitivas com dimensões, simetria, variação e id desconhecido | PASS | C6-C9 (dimensões), C13-C16 (espelhamento), C11/C18 (passo de clareamento, rugosidade), C19 (id inexistente) reprovam |
| 14 | Desempenho sem regressão | PASS | `stress:500` 77,2 / 76,0 / 76,3; catálogo só no snapshot, nada no tick |

## Mutações (todas reaplicadas por mim e revertidas com `git checkout -- <arquivo>`)
Servidor, 50 mutantes válidos: 46 mortos, 4 sobreviventes (abaixo). L6/L7/L9/W2 não compilavam na 1a forma; refeitos de forma válida e mortos.
Cliente, 29 aplicadas: 27 mortas. Em C3, C13, C14, C20, V1, V3, V6, V7 o motor também emitiu SCRIPT ERROR, mas testes específicos reprovaram por nome; contadas como mortas. Sobreviventes: C12, V4.
`git status` ao final: somente este arquivo não rastreado.

## Sobreviventes (menores, sem FAIL)
- C12 (`variation >= 0` em `object_factory._material_for`): equivalente, `lightened(0.05 * 0)` é identidade.
- V4 (leito de hospedagem com variação 1 em `interior_view`): leito de vaga sempre variação 0 não é critério declarado.
- L16 (offset padrão da parte `[0,1,0]` quando omitido): default de campo sem critério explícito.
- D3 (`ObjectVariation.Count` 4 -> 3): tamanho do intervalo de variação, sem critério (determinismo e seed por peça estão protegidos).
- O4, O7 (categoria `bed` e `anvil` em `DefaultObjects`): categoria lida pelo loader está protegida (L12-L14); o valor padrão de duas peças não.

## Lacunas apontadas por verifiers anteriores
Fechadas e confirmadas: seed por peça (P2), simetria/categoria no loader (L4, L14, L12/L13), dimensões por forma (L3*), validador (W4/W5), passo de variação do cliente (C11).
