# Causal provenance audit [Estado: Implementado, aguardando verificação independente (issue #113 fatia A)]

## Problem

Princípios 3 e 9: toda transformação relevante precisa de causa reconstruível. O motor já tem
`CauseEventId` + `SourceSystem` (AD-013), mas quase nenhum emissor passa causa e não existe lista
do que **deve** ter causa. Sem essa lista nenhum gate (fatia D) consegue reprovar nada, e a
"história da sociedade" (#125) não explica transformação alguma.

Esta fatia entrega só o inventário e a regra: auditoria por kind, `CausalPolicy` no código e um
teste que reprova kind sem classificação. Nenhum emissor muda.

## Flow

1. `docs/audits/causal-provenance-audit*.md` (new) - 67 kinds: emissor, fonte, causa, causa proximal
2. `CausalPolicy` (new) - tabela no código: transformação ou raiz
3. `CausalPolicyTests` (new) - reflexão sobre o enum (R5)
4. `docs/decisions-log.md` (exists) - AD-121 com a regra de causa

## Rule (AD-121)

*Causa = o evento registrado mais próximo que tornou a transição possível.* Condição sem evento
(comida da cidade zerada, aluguel atrasado) vira **um evento de transição de condição por
cidade/household quando começa** (e outro quando termina), nunca um por NPC por tick; custo
medido em `tests/baselines/event-log-bytes.json`. Propostas na auditoria; criar é do dono.

## Acceptance criteria (EARS)

| Id | Critério | Prova |
|---|---|---|
| AC1 | WHEN um valor de `WorldEventKind` não está em `CausalPolicy`, THE teste SHALL falhar listando-o | `Every_world_event_kind_is_classified_as_root_or_transformation` |
| AC2 | THE `CausalPolicy` SHALL ter cada kind em exatamente uma lista, só kinds definidos | `Policy_lists_contain_only_defined_kinds_without_overlap` |
| AC3 | THE auditoria SHALL cobrir os 67 kinds com emissor `arquivo:linha`, fonte, causa e causa proximal | revisão do verifier por amostragem de `arquivo:linha` |
| AC4 | THE fatia SHALL NOT alterar o estado do mundo | hash canônico e goldens inalterados em `verify.sh` |
| AC5 | THE docs SHALL ter no máximo 100 linhas cada | `bash scripts/check-docs.sh` |

Mutação sugerida ao verifier: remover um kind de `CausalPolicy.Roots` ou `Transformations` e ver
AC1 reprovar; duplicar um kind nas duas listas e ver o construtor estático lançar (AC2).

## One-way doors

Nenhuma. `CausalPolicy` é só leitura, sem estado, sem contrato público, sem persistência.
Fatias B, C e D usarão `CausalPolicy.IsTransformation` como fonte da verdade.

## Out of scope

- Fatias B, C e D da #113; qualquer mudança de emissor, `NpcDeath`, ou remoção do overload legado.
- Criar eventos de condição (decisão do dono: eles mudam goldens e `NextHistoryEventId`).
- Mostrar cadeias ao usuário (#125); payload tipado.

## Performance

Sem sistema novo, sem mudança no tick: nenhum custo por NPC-tick. `compare-simulation-cost.sh`
**não foi rodado** de propósito: outras worktrees rodam em paralelo e a medição sairia com ruído;
o orquestrador mede uma vez, ao final, após a integração.

## Open points for the owner

- `SettlementFounded` não tem emissor; migração não emite evento (ver auditoria).
- Quais eventos de condição criar, e se por cidade ou por household.
- Roots e transformações de fronteira (`StillBirth`, `ExtraordinaryAcquired`, `Trade`) foram
  decididas aqui e podem ser reclassificadas trocando uma linha na lista.
