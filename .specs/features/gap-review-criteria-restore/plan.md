# Gap review criteria restore

## Problem

`docs/audits/phase-gap-review.md` lista critérios de fases fechadas que ficaram mais fracos que o roadmap sem AD registrando o desvio: histerese 20/20 virou média com folga, preço sob escassez virou "nunca menor e maior algum dia", gates de 10 anos viraram 1 mês com amostragem, testes de "20 seeds" são vácuos (nenhum sistema de skill lê RNG) e a herança de skill é tautológica. O mesmo documento diz que o agente rodou só análise estática.

Enquanto isso, "fechada" no `ROADMAP.md` promete mais do que o gate prova. Decisões já tomadas (AD-082, AD-083, AD-088) não se revertem: o trabalho é, item a item, ou reforçar o teste, ou registrar o desvio em AD.

Quando isto entrar, cada critério fraco da lista ou prova o que o roadmap diz, ou tem um AD que explica por que o roadmap mudou.

## Flow

Só testes e documentação; nenhuma regra de simulação muda.

1. `phase-gap-review.md` (exists) - fonte da lista "Abertas - critério mais fraco"
2. testes das suítes Unit e LongRunning (exist, extended) - reforçados, ou removidos com AD se vácuos
3. `docs/decisions-log.md` (exists, extended) - AD para cada desvio mantido
4. documentos de fase do roadmap (exist, extended) - critério atualizado quando o desvio é decidido

## Impact

| Front | What changes |
| --- | --- |
| domain | nenhum termo novo; critério passa a ser o que o teste prova |
| stored data | none - só testes e docs; hash canônico idêntico |

## Relations

None - nenhuma entidade nova.

## Surface

None - nothing consumed outside

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. fonte de verdade do critério | cada linha da lista vira: teste reforçado, ou AD mais critério reescrito no roadmap | deixar o texto do roadmap e o teste divergentes: é o estado atual |

- Nada mais aqui Ã© difÃ­cil de reverter

## Criteria

### S1: Critérios fracos resolvidos (P1)

**Acceptance Criteria**

1. WHEN um item da seção "critério mais fraco" é fechado THEN the system SHALL ter teste que reprova a mutação do comportamento ou um AD que registra o desvio
2. IF um teste de "20 seeds" não lê RNG THEN the system SHALL ser trocado por teste que varia a seed de fato ou removido com AD
3. The system SHALL manter o gate rápido em até 110% do tempo atual (~1 min)

**Independent test:** rodar `bash scripts/verify.sh` e a mutação do item.

### S2: Lista atualizada (P2)

**Acceptance Criteria**

4. WHEN todos os itens fecham THEN the system SHALL mover a seção para "Fechadas" com o AD ou o teste citado
5. The system SHALL manter golden de hash inalterado

**Independent test:** diff do audit e golden verde.

## Out of scope

| Excluded | Why |
| --- | --- |
| FAM-32 e migração "mesmo tick" | decisão D pendente: muda critério ou modelo, é AD |
| remover `ExampleCounterSystem` | muda todos os goldens |
| recursos e profissões novos | muda calibração (AD-046) |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| ordem | F4, F5, F6 e F7 primeiro | são os de teste vácuo ou tautológico | n |
| horizonte de 10 anos | só o gate `--nightly` | AD-083 e `longrunning-budget.md` | n |

**Open questions:** none - all resolved or logged above.

## Observable

None - no user-facing surface

## Sources

- `docs/audits/phase-gap-review.md`
- AD-082, AD-083, AD-088 em `docs/decisions-log.md`
- `rules/eval-criteria.md`
