# family-diversity-calibration — checks

Profile: standard
Plan: `.specs/features/family-diversity-calibration/plan.md`

## Checks

**C1** - Faixa de Vitality medida em 20 seeds, sem literal de tolerância (AC 1)
Proof: `bash scripts/test.sh --project longrunning --filter FamilyDiversityCalibrationTests.Real_vitality_difference_stays_in_the_calibrated_band`

**C2** - Controle negativo sem seleção direta (AC 2)
Proof: `bash scripts/test.sh --project longrunning --filter FamilyDiversityCalibrationTests.Unselected_trait_stays_in_the_neutral_band`

**C3** - Seleção forte reprovada, controle atual aprovado (AC 3)
Proof: `bash scripts/test.sh --project longrunning --filter FamilyDiversityCalibrationTests.Strong_selection_is_rejected_by_the_same_band`

**C4** - CV sem colapso até 100 anos (AC 4)
Proof: `bash scripts/test.sh --project longrunning --filter FamilyDiversityCalibrationTests.Vitality_does_not_collapse_at_century_checkpoints`

**C5** - Critério documentado coincide com teste e FAM-32 é verificado (AC 5)
Proof: `bash scripts/test.sh --project longrunning --filter FamilyDiversityCalibrationTests.Documented_criterion_names_the_executable_proofs`

**C6** - Favoritos incompatíveis não bloqueiam parceiros válidos (AC 6)
Proof: `bash scripts/test.sh --filter An_incompatible_favourite_does_not_block_a_compatible_partner`

**C7** - Convivência no turno cria vínculos externos à família (AC 7)
Proof: `bash scripts/test.sh --filter Coworkers_meeting_only_during_work_hours_form_relationships_through_the_world_clock`

**C8** - Nascimento e casamento preservam a cidade (AC 8)
Proof: `bash scripts/test.sh --filter A_newborn_keeps_the_mothers_city`
Proof: `bash scripts/test.sh --filter A_married_household_keeps_its_heads_city`

**C9** - Pretendentes já escolhidos mantêm exclusividade e contatos direcionados (AC 9)
Proof: `bash scripts/test.sh --filter A_one_way_relationship_counts_as_contact_in_either_direction_before_later_seekers`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| atributos (2) | Vitality C1 · controle negativo C2 | - |
| seleção (2) | atual C1 · forte C3 | - |

## Test policy

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decisões observáveis | integração na borda e teste local quando houver decisão interna | todos os resultados declarados |
| Instrumentação | prova do consumidor | sem teste espelho |

## Swept

- validation: C3
- failure modes: C3
- idempotency: n/a - sensor estatístico sem comandos persistentes
- authorization: existing - API local segue política dos endpoints atuais
- concurrency: n/a - cada cenário isolado por seed
- data lifecycle: C1 - baseline versionado explícito
- dependency failure: n/a - cenário sem dependência externa
- state transitions: C3
- observability: C1 - resultado por seed no baseline

## Handoff

- Estimativa: fontes relevantes < 400 KB / 4 = <100k tokens; sob orçamento de 150k, um builder.
- Decisões humanas: opção B (#65), SQLite por mundo e seed duplicada 409 (#67).
