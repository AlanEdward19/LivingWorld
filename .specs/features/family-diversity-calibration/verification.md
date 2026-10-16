# Family diversity calibration verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 10bec92325c274f9f630c69fea83062974d73779..abbefa5
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier), 2026-10-16 UTC

## Binding sources

Li diretamente issues #65/#67 (`gh issue view`), ADR-0034, planos/checks e roadmap.
A faixa, controle negativo, piso anual e escopo demográfico coincidem; nenhuma contradição.

## Checks

Prefixos: LR=`tests/LivingWorld.Tests.LongRunning/Population/`; U=`tests/LivingWorld.Tests.Unit/Population/`.
Provas executadas nos scripts originais em HEAD; F1–F4 abaixo são mutações no scratch.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | 20 seeds, faixa medida | FamilyDiversityCalibrationTests | LR/FamilyDiversityCalibrationTests.cs:30 `Assert.True(InBand(Current.Value.Select(s => s.VitalityDifference), ...))` | PASS |
| C2 | Openness, IC contém zero | mesma classe | LR/FamilyDiversityCalibrationTests.cs:39 `Math.Abs(differences.Average()) <= Baseline().ConfidenceZ * StandardError(differences)` | PASS |
| C3 | seleção forte rejeitada | mesma classe | LR/FamilyDiversityCalibrationTests.cs:48 `Assert.False(InBand(strong.Select(...), ...))` e atual `Assert.True` | PASS |
| C4 | século completo, CV e população positivos | mesma classe, 20 seeds × 100 anos | LR/FamilyDiversityCalibrationTests.cs:61 `s.MinimumVitalityCv >= floor && s.MinimumAlive >= populationFloor` | PASS |
| C5 | documentação igual ao gate | comparação direta + Documented_criterion | docs/roadmap/phase-07-family.md:50–57 especifica média ± z×EP, IC de Openness e mínimos anuais, como `InBand` em LR/FamilyDiversityCalibrationTests.cs:92 | PASS |
| C6 | favorito incompatível não bloqueia | An_incompatible_favourite, 3 casos | U/Family/CourtshipSystemTests.cs:153 `Assert.Equal(compatible.Id, seeker.CourtingWith)` e :154 inválido nulo | PASS |
| C7 | contato no turno | Coworkers_meeting_only | U/Family/RelationshipSystemTests.cs:117 `Assert.True(world.Relationships.ContainsKey(new RelationshipKey(a.Id, b.Id)))` | PASS |
| C8 | cidade de nascimento/casamento | A_newborn_keeps + A_married_household_keeps | U/Lifecycle/NatalitySystemTests.cs:126 `Assert.Equal(city, baby.City)`; U/Family/MarriageSystemTests.cs:95 `Assert.Equal(city, ...City)` | PASS |
| C9 | exclusividade, contato em ambos sentidos | A_one_way_relationship_counts, 2 casos | U/Family/CourtshipSystemTests.cs:121 `Assert.Equal(candidate.Id, first.CourtingWith)` e :122 `Assert.Null(later.CourtingWith)` | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| atributos (2) | ADR-0034 e Measure | Vitality C1; Openness C2 | - |
| seleção (2) | parâmetro de Measure | atual C1/C3; peso 20 C3 | - |
| favoritos inválidos (3) | CourtshipSystemTests.cs:125–128 | irmão, mesmo sexo, fora da fertilidade C6 | - |
| checkpoints (101 por seed) | Measure: inicial e anos 1..100 | C4 calcula mínimo de todos | - |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decisões observáveis | courtship, relacionamento, nascimento e casamento | regras locais + relógio real e cenário | yes |
| Instrumentação | calibração e baselines | consumidor reprova seleção forte e colapso | yes |

Swept existing: autorização local continua a mesma; baseline é versionado e nenhuma gravação é feita sem flag explícita.
FAM-33 e contrafactuais: LR/FamilyPairedScenarioTests.cs:277 `onHigh < offLow`, :337 `envDistance >= geneDistance`, :372 `low > 0`; todos executados.

## Faults injected

| Mutation | Location | Killed |
| --- | --- | --- |
| F1: seleção real peso 20 | FamilyDiversityCalibrationTests.Measure | yes - C1 |
| F2: diferença de Openness deslocada +0,1 | Measure | yes - C2 |
| F3: ignorar parâmetro da seleção forte | Measure | yes - C3 |
| F4: cortejo desligado no cenário completo | Measure | yes - C4 |
| F6real: remover filtro de compatibilidade | CourtshipSystem.PickBestByAttraction | yes - C6 |
| F7: voltar a frequência para Daily | RelationshipSystem | yes - C7 |
| F8: remover cidade de bebê e de domicílio, separadamente | NatalitySystem / MarriageSystem | yes - C8 (duas execuções) |
| F9: remover revalidação do pretendente | CourtshipSystem | yes - C9 |

O ensaio inicial no filtro antigo do braço neutro não exercitava C6; foi substituído pelo mutante F6real no código novo.
Scratch isolado `/tmp/lw-verifier-mutants`; árvore real permaneceu limpa até a escrita destes relatórios.

## Gate

`bash scripts/verify.sh`: exit 0, 2298 passed / 3 skipped; `bash scripts/client-test.sh`: exit 0, 258 passed.
`bash scripts/test.sh --project longrunning --filter 'FamilyDiversityCalibrationTests|FamilyPairedScenarioTests|DeterminismTwoProcessTests|ScaleScenarioSensorTests'`: 32 passed / 2 skipped, 17m27s; século completo e FAM-33/contrafactuais verdes.
Integração: `--filter 'SavedWorld|ZeroRoundTripsTests'` 10 passed; `--filter 'ApiContractTests|WorldCreateEndpointsTests|PositionalSnapshotPersistenceTests'` 27 passed. Ambos por `bash scripts/test.sh --project integration`.
`bash scripts/compare-creation-cost.sh /tmp/lw-verifier-base /tmp/verifier-creation-cost`: 2355,396 → 1929,044 ms (razão 0,819, limite 1,05).
`bash scripts/compare-simulation-cost.sh /tmp/lw-verifier-base /tmp/verifier-simulation-cost`: 2,694 → 1,846 µs/NPC-tick; 255402 → 256268 B/tick (+0,339%, limite +2%).
`bash scripts/client-stress.sh /tmp/lw-verifier-base/client/godot` e `bash scripts/client-stress.sh`: FPS 73,2/71,4/73,1 → 73,3/74,0/72,6; medianas 73,1 → 73,3. Carga concorrente: números não demonstram aceleração.
Logs próprios em `/tmp/verifier-*`; medições por 3 pares/rodadas, base 10bec92. `WorldCreationCostTests` foi copiado à base somente como instrumentação, sem alterar o motor. Árvore real limpa antes/depois das mutações.

## Status após aprovação

Se aprovado, marcar plano verificado e FAM-32 Verified em `spec/traceability.md`; manter a fase Parcial por FAM-06/20/22.
Atualizar índice da spec, roadmap/STATE para refletir FAM-32 verificado; nenhuma aprovação foi antecipada aqui.
