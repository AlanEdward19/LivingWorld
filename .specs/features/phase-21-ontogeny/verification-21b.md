# Verificação 21b (issue #71) — Verifier independente

Branch `feat/ontogeny-acquisition` (525f585). Veredito: **PASS em todos os critérios** (3 observações não bloqueantes abaixo).

| # | Critério | Veredito | Evidência própria |
|---|---|---|---|
| 4 | Cuidador ausente → menos marcos em >=18/20 | PASS | `scripts/test.sh --filter OntogenyAcquisitionTests`: 7/7 passam; o teste `A_child_without_a_caregiver...` asserta `fewer >= 18` sobre 20 seeds (OntogenyAcquisitionTests.cs:83-98). Mutação: `HasCaregiver` retornando sempre true (ExposureAccumulator.cs:14) reprova esse teste (3 falhas: critério 4, ordem da janela, teto). Restaurado. |
| 5 | prazo > tardia > nenhuma em >=18/20; tardia < prazo sempre | PASS | Teste `Window_order_is_strictly...` (linhas 115-126) asserta por seed `late < onTime` e `ordered >= 18`. Mutação: anular `ceiling = ...MissedWindowKeep` e `progress = Math.Min(progress, ceiling)` em `OntogenyRules.Advance` (linhas ~143-144) reprova `Window_order_is_strictly...` e `The_ceiling_only_ever_goes_down...` (2 falhas). Restaurado (`git checkout`). |
| 6 | Gene: idêntico -> byte-idêntico; diferente -> idade de aquisição difere | PASS | `Identical_genes_and_exposure...` (20 seeds): `Assert.Equal` do `Development` (record) e `Assert.NotEqual` dos meses até `Language>=0.4` com rate x1,25. Passa. Sem sorteio em Advance (puro). |
| 10 | Sem Random/DateTime/Guid; determinismo em 2 processos; golden | PASS | `grep -nE "Random\|DateTime\|Guid"` em todos os `.cs` de Ontogeny (exceto testes): 0 ocorrências. `dotnet src/LivingWorld.Workers/bin/Release/net10.0/LivingWorld.Workers.dll hash 1 2160` duas vezes: `b1d1c9ec...e2b3;a0fa96ff...efb3` nas duas. `verify --full` cobre golden. |

## Itens adicionais
- Snapshot de adulto: PASS. `StateFor`/`AdvanceOneMonth` devolvem `null` ao completar (OntogenyMechanic.cs); teste do 21a (`OntogenyModelTests` ~242) asserta `Assert.Null` para adultos. Crescimento medido: 12.267 -> 12.364 B/vivo (+0,8%), vindo dos 6 tetos das crianças/déficits, não de adultos completos.
- Teto só desce: PASS (`The_ceiling_only_ever_goes_down...` e `progress <= ceiling`; reprova na mutação 1).
- Déficit permanente: PASS. `AdvanceOneMonth` mantém o estado se `now >= CompleteAtYears` e `!IsComplete`; ADR-0029 documenta o trade-off.
- Parâmetros validados: PASS. `Create` rejeita `LateRecoveryFactor` fora de (0,1] e `MissedWindowKeep` fora de (0,1) (OntogenyRules.cs:53-56); teste `Invalid_recovery_and_keep...`; loader e conversor JSON os leem. Documentados em docs/domain/period-authoring-schema.md:84-87.
- ADR-0029 x código: PASS (exposição binária por adulto vivo no domicílio, sem sorteio, teto = p+(1-p)*Keep, 6 tetos, déficit mantém estado). AD-116 coerente.
- Docs: PASS. `bash scripts/check-docs.sh` -> `check-docs: OK`; ADR 31 linhas, schema 91, plan 61.
- Desempenho (MeasureHarness, 3 rodadas alternadas base/nova, mesma máquina):
  - µs/NPC-tick base 0,826 / 0,779 / 0,791 (média 0,799); nova 0,827 / 0,782 / 0,775 (média 0,795) = **99,5%** (teto 103%). Ruído entre rodadas da mesma base ~6%, maior que o efeito.
  - alocação/tick base 251.042 / 250.993 / 250.926; nova 255.025 / 254.885 / 254.882 = **101,6%** (teto 102%; estável, ruído <0,1%).
- `bash scripts/verify.sh --full`: **exit 0** (`verify: OK`; LongRunning 128 passou, 8 skipped de gravação de baseline).

## Observações (não bloqueiam)
1. A issue cita "Resolver perfil Agregado"; a entrega substitui por gene determinístico (ADR-0029, exigência do critério 6). Decisão registrada, coerente com o plan.md.
2. Borda: com janela de marco cujo limite excede `CompleteAtYears` (ex.: `social-adult`, limite 18 > 13) e eixo ainda abaixo de 1 sem teto reduzido, `AdvanceOneMonth` congela o estado em `now >= CompleteAt` e a criança com cuidador tardio não completa esse último trecho (nem o teto desce). Efeito pequeno, sem teste; considerar na 21c.
3. A mutação do critério 5 do próprio teste (`WindowLimitYears=99`) é complementar; a mutação no código real foi feita aqui e reprova.

Nenhum arquivo além deste foi alterado (mutações restauradas; `git status` só mostra este arquivo).
