# ADR-0009: `BranchId` no esquema e no hash desde a Fase 3

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O ADR-0008 estabelece ramificação como modelo temporal, mas a fase que a implementa vem
depois da 8 — muitos meses de trabalho à frente. A pergunta é quando pagar pela dimensão
de linha temporal no esquema.

A assimetria é brutal. Agora: uma coluna, um parâmetro e uma chave composta, num banco
vazio. Depois: migração em **todas** as tabelas de um mundo já populado, mais reescrita do
hash, dos índices, das consultas e dos snapshots existentes — com dados reais em risco.

Isto não é abstração especulativa. O requisito está declarado; só a implementação está
adiada. YAGNI protege contra necessidade imaginária, não contra necessidade agendada.

## Decisão
Vamos incluir `BranchId` desde a Fase 3, mesmo sem nenhum código de ramificação:

- Toda entidade persistida e toda linha de event log carregam `BranchId`.
- Chave primária composta e índices já contemplam a coluna.
- O hash canônico do mundo é **por branch**.
- Toda consulta e todo repositório recebem o branch como parâmetro, nunca implícito.
- Até a fase temporal existe **exatamente um branch**, o raiz. Nada ramifica.
- Um teste de arquitetura reprova qualquer consulta que não filtre por `BranchId` — é o
  que impede o campo de virar decoração esquecida que não funciona quando for usada.

## Alternativas consideradas
- **Aceitar o retrofit** — Fases 0–14 mais enxutas, e uma migração grande e arriscada
  depois, sobre um mundo populado. O tipo de dívida que trava um projeto por semanas.
- **Só a chave lógica, sem propagação** — reservar a coluna e não passá-la adiante. Mais
  barato ainda, e falha no que importa: no dia da virada, todas as consultas continuam
  ignorando o branch e o bug é silencioso, não um erro de compilação.

## Consequências
- **Positivas**: a fase temporal deixa de exigir migração; o isolamento por branch é
  verificado por teste desde cedo; multi-mundo no mesmo banco fica de graça, o que ajuda
  teste e cenário paralelo bem antes de existir viagem no tempo.
- **Negativas / trade-offs**: toda assinatura de repositório fica um parâmetro mais longa,
  para sempre; índices ficam um pouco maiores; um campo que não faz nada por muitas fases
  é exatamente o tipo de coisa que alguém "limpa" — daí o teste de arquitetura.
- **Follow-ups**: a Fase 3 ganha a task e o critério; `rules/database-entities.md` e
  `rules/simulation-determinism.md` registram a regra.
