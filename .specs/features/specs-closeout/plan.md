# Specs closeout

## Problem

Existem 37 diretórios em `.specs/features/` no formato antigo sem `checks.md` nem `verification.md`, entre eles `phase-04` a `phase-13`, `phase-15*`, `phase-16*`, `phase-17..23`, `phase-28`, `city-house-layout`, `directional-city-bounds-growth`, `dynamic-city-growth`, `npc-authoring-tools`, `emergent-vehicles`, `trilha-c-dwarf-fortress-worldgen` e `dead-code-audit`. As specs `phase-04..11` ainda têm tabelas "Pending" mesmo com a fase fechada, e `tasks.md` de fases entregues é efêmero que deveria ter sido limpo (`AGENTS.md`).

Quem abre o repo não sabe o que é entregue, adiado ou bloqueado sem ler código. Evidência: `ls .specs/features`, `docs/audits/phase-gap-review.md` (seção D).

Quando isto entrar, cada diretório de spec diz em uma linha se está entregue, adiado ou bloqueado, e os efêmeros somem.

## Flow

Trabalho só de documentação; confere cada tabela contra o código antes de marcar.

1. as specs de feature (exist) - tabela de rastreio vira Verified, Deferred ou Blocked com evidência
2. `tasks.md` de fase entregue (exist) - removido, fora do que `AGENTS.md` preserva
3. `.specs/STATE.md` (exists) - uma linha por diretório com o estado
4. `ROADMAP.md` (exists) - status só muda se o critério da fase fechar

## Impact

| Front | What changes |
| --- | --- |
| domain | nenhum termo novo |
| stored data | none - só docs; apagar `tasks.md` exige a política de limpeza |

## Relations

None - nenhuma entidade nova.

## Surface

None - nothing consumed outside

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. apagar efêmero | remover `tasks.md` e `validation.md` só de fase com `verify.sh` verde e spec marcada | apagar tudo de fases `spec`: perde o plano das fases 17..23 |

- Nada mais aqui Ã© difÃ­cil de reverter

## Criteria

### S1: Rastreio honesto (P1)

**Acceptance Criteria**

1. WHEN uma linha de rastreio é marcada Verified THEN the system SHALL citar o teste ou o arquivo que a prova
2. IF o código não prova a linha THEN the system SHALL marcá-la Pending com o motivo
3. The system SHALL conter em cada diretório `.specs/features` ao menos `spec.md` ou `plan.md` com o estado declarado na primeira linha

**Independent test:** script que lista diretórios sem estado declarado retorna vazio.

### S2: Efêmeros limpos (P2)

**Acceptance Criteria**

4. WHEN a fase está fechada THEN the system SHALL não ter `tasks.md` dela no repositório
5. The system SHALL manter `AGENTS.md`, `rules/`, `docs/`, `spec.md` e `design.md` intactos

**Independent test:** `git status` mostra só remoções de efêmeros e edições de tabela.

## Out of scope

| Excluded | Why |
| --- | --- |
| implementar qualquer fase `spec` | cada uma tem plano próprio |
| converter specs antigas ao formato lean | só se a feature for retomada |
| renomear ou apagar spec e design | AGENTS.md manda perguntar |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| escopo do apagar | só `tasks.md` e `validation.md` de fases fechadas | regra de limpeza do `AGENTS.md` | n |
| ordem | 04 a 11 primeiro | são as com tabela Pending | n |

**Open questions:** none - all resolved or logged above.

## Observable

None - no user-facing surface

## Sources

- `AGENTS.md` - política de limpeza
- `docs/audits/phase-gap-review.md` - seção D
- `.specs/features/`
