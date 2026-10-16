# Lodging and rent — verificação independente

**Verdict**: FAIL
**Profile**: light (AD-105); obrigações adicionais da issue #67 executadas abaixo.
**Diff range**: 8f046f9^..8f046f9; provas na árvore 10bec92.
**Round**: 1 — full; **Verifier**: independent sub-agent (author != verifier).
Verificado em 2026-10-15. Nenhum código produtivo alterado na árvore real.

## Binding sources

Issue #67 Parte 1 aberta por `gh issue view 67 --json title,body,state,assignees` (exit 0,
com escalada após rede indisponível no sandbox); AD-104..107 e `git show 8f046f9` lidos.
A issue pede 19 ACs; o plano enumera 17: 1–7, 10–19. AC8/9 ausentes, não inventados.
`checks.md` ausente: caminho legado/degradado, provas derivadas diretamente dos ACs.
Inconsistência comunicada ao orquestrador, que registrou comentário na issue antes de continuar.

## Comandos e saídas próprias

- A: `bash scripts/test.sh --all --filter 'LodgingSystemTests|CityProjectorTests'`: exit 0, 39 passed, 0 failed.
- B: `bash scripts/client-test.sh`: exit 0, 252 passed, 0 failed; inclui nominalmente
  `test_asset_catalog::test_lodging_building_shows_one_bed_per_unit_and_a_price_line` e
  `test_ui_modules::test_hover_text_comes_from_store_and_server_labels`.
- C: `bash scripts/test.sh --project longrunning --filter 'FullyQualifiedName~DeterminismTwoProcessTests.Same_seed_produces_identical_hashes_across_two_separate_processes|FullyQualifiedName~DeterminismTwoProcessTests.Different_seeds_produce_different_canonical_hashes_across_processes'`:
  exit 0, 2 passed, 0 failed. Cenário padrão, sem hospedagem: não prova AC18 com sistema ativo.
- D: `bash scripts/verify.sh`: primeira tentativa falhou por artefatos concorrentes de MSBuild;
  repetição sequencial exit 0, `verify: OK`, docs/build/lint verdes; Unit 2271 passed, 3 skipped, 0 failed.
- Logs completos locais: `/tmp/lodging-{focused,client,two-process,verify,mutation-debit,mutation-grace,cost}.log`.

## Checks

`L` = `tests/LivingWorld.Tests.Unit/Cities/LodgingSystemTests.cs` (provas A).
FAIL de cobertura significa obrigação não demonstrada, não necessariamente código incorreto.

| Check | Claim | Proof run / Evidence | Result |
| --- | --- | --- | --- |
| AC1 | débito e crédito conservam moeda | A; L:118 `Equal(400, Wallet.Amount)`, :119 crédito, :120 `Equal(before, TotalMoney(world))`; mutante débito morto | PASS |
| AC2 | atraso + RentUnpaid | A; L:132 `Equal(1, RentArrearsMonths)`; `rg -n 'RentUnpaid' L` não encontra assert do evento | FAIL |
| AC3 | despejo após carência + tick + evento | A; L:133 preserva dwelling, :137 `Null(Dwelling)`; mutante carência morto; não há assert de EvictedAtTick/evento | FAIL |
| AC4 | vaga mais barata, desempate id, capacidade, localização | A; L:202 `Equal(3,lodged.Count)`, :203 ids, :205 localização; cenário tem uma estalagem, não compara preços/empate entre prédios | FAIL |
| AC5 | eficiência do chão | A; L:154 `Equal(RestPlaceKind.Ground,rest.Kind)`; eficiência de recuperação não afirmada | FAIL |
| AC6 | despejado precisa migrar | A; L:186 `Equal(elsewhere.Id,evicted.PendingRelocationCity)` e :188 `Null(housed.PendingRelocationCity)` | PASS |
| AC7 | sem Lodging mantém hash fora campos novos | A; L:231–233 verifica dinheiro/dwelling/atraso, nenhum hash anterior normalizado; pins regravados não provam equivalência | FAIL |
| AC10 | fórmula exata e limites | A; L:290 `Equal(10000,ValueOf(...))`, :306 `Equal(150,crowded)`, :312 piso 50; centralidade só >= (:328), sem assert waterfront/fórmula completa | FAIL |
| AC11 | pressão teto/vaga e população/vaga trabalho | A; L:306 teto sob pressão; estabelecimento só `InRange(value,2500,10000)` (:360), razões exatas não afirmadas | FAIL |
| AC12 | aluguel atual dinâmico ou fixo | A; L:375 `Equal(800,Wallet.Amount)`, :376 crédito 200; fixo em L:118–120 | PASS |
| AC13 | longe de casa dorme e paga ao fim da noite | A; L:417 Lodging e :423 crédito 40/:424 conservação; :422 chama TryCharge diretamente, não atravessa fim da noite | FAIL |
| AC14 | sem diária/vaga/dinheiro usa descanso anterior | A; L:432,437,442,451 `NotEqual(RestPlaceKind.Lodging,Resolve(...).Kind)` | PASS |
| AC15 | tooltip vagas/aluguel/diária/valor | B; client/godot/tests/test_ui_modules.gd:161 contém quatro valores; :163 verifica estabelecimento | PASS |
| AC16 | um leito por vaga limitado só ao piso livre | B; client/godot/tests/test_asset_catalog.gd:146 só verifica quatro leitos; código interior_view.gd:16,93 impõe teto adicional 12 | FAIL |
| AC17 | rejeita todos dados incoerentes | A; L:104 aluguel zero, :390 Min>Max; `rg -n 'GraceMonths|NightlyRate' L` mostra ausência de testes negativos das outras duas fronteiras | FAIL |
| AC18 | determinismo dois processos com hospedagem | A+C; L:249 `Equal(Outcome(),Outcome())` é mesmo processo e resumo parcial; C não ativa hospedagem | FAIL |
| AC19 | zero custo sem dados; 6000 NPCs <= +5% | probe scratch abaixo: 1200 B/chamada sem Lodging/Pricing; não existe comparação própria 6000 antes/depois | FAIL |

## Coverage e nível

Recomputados: AC17 tem quatro famílias vinculantes (Rent<=0, Grace<0, Pricing incoerente,
Nightly sem distância): só duas provas. AC16 exige todos os tamanhos até piso livre; teste cobre 4,
não >12. AC4 precisa múltiplos prédios/preços/ids; teste cobre uma estalagem.
AC2/3 exigem eventos e tick observáveis, ausentes nos asserts. AC13 exige fronteira do relógio.
Não há `Test policy`/`Swept existing` no artefato legado. Não há mock/layout vinculante;
visual enumerado: tooltip com vagas, aluguel, diária, valor e camas no interior.

## Faults injected

Scratch isolado: `git worktree add --detach /tmp/lodging-mutant HEAD`; baseline real limpo.
- Remover débito (`TryDebitWallet` -> comparação de saldo): `bash scripts/test.sh --filter Rent_moves_money_from_the_head_to_the_landlord_without_creating_or_losing_any`:
  exit 1, 1 failed, L:118 esperado 400/recebido 500. **Killed: yes**.
- Ignorar carência (`arrears <= terms.GraceMonths` -> `arrears <= 0`):
  `bash scripts/test.sh --filter Unpaid_rent_builds_arrears_and_eviction_comes_only_after_the_grace_months`:
  exit 1, 1 failed, L:132 esperado 1/recebido 0 (Evict limpa atraso). **Killed: yes**.
Outros ACs causais/fronteira não tiveram mutação individual: lacuna frente à regra geral 10 da issue.

## Custo e gate

Probe somente no scratch, comportamento produtivo restaurado: 10 aquecimentos, 100 chamadas mensais
em `WorldWithInn(declareLodging:false)`, GC.GetAllocatedBytesForCurrentThread antes/depois.
`bash scripts/test.sh --filter Verifier_no_lodging_cost_probe`: exit 1, esperado 0, real 120000 bytes.
`LodgingSystem.cs:30–40` cria Dictionary e ordena/varre oficinas/prédios antes do early return.
Ausência de medição comparativa própria da criação de 6000 NPCs permanece FAIL, sem adotar relato AD-105.
`python3 /Users/aoliveira/.codex/skills/tlc-spec-lean/scripts/validate_verification.py lodging-and-rent`: exit 1, verdict FAIL; nenhum PASS inferido.
Scratch descartado; `git status --porcelain` real difere do baseline só pelo relatório novo.
