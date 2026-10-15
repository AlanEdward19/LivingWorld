# ADR-0029: Acolhimento de órfãos, choro atendido, exposição graduada e criação adotiva

- **Status**: aceito
- **Data**: 2026-10-15
- **Decisores**: Alan (pediu quitar as dívidas da Fase 21, issue #93)

## Contexto
A Fase 21 fechou com cinco dívidas (ADR-0028). Uma sonda em 5 seeds × 10 anos achou 9 mortes por desidratação, todas de criança órfã sozinha
num domicílio unitário: `HouseholdRedistribution` só acolhe com avós ou irmão adulto, e sem parente a criança fica só.

## Decisão
- **Acolhimento** (`OrphanCareSystem`, mensal): criança sem adulto/idoso vivo no domicílio é levada por parente (avós, irmão adulto, tios),
  senão por quem tem afeto ≥ 50 por ela, senão pela comunidade da própria cidade (casa menos cheia, depois maior altruísmo, depois menor id).
  Evento `OrphanTakenIn`. Morte dos pais dispara depois do sistema no último tick: até um mês de espera é esperado.
- **Choro atendido**: adulto na mesma célula de uma criança da casa que chora, com a despensa sem comida ou água e carteira, compra mesmo na rotina de dormir.
- **Exposição graduada**: `AttentionPerCaregiver` (cenário, padrão 1,0 = binária); `1 − Π(1 − atenção)`; o ganho e a recuperação do mês escalam por ela.
- **Criação adotiva**: `Npc.Upbringing` ganha mutador; enquanto a criança cresce caminha para a média dos cuidadores que **não são os pais**,
  ponderado pela proporção deles, pela exposição e pela fração do desenvolvimento do mês. Com os pais criando, nada muda.
- **Na hora e no casamento**: `HouseholdRedistribution` tenta o acolhimento antes de criar domicílio unitário (os órfãos morriam em horas, antes do
  sistema mensal) e `MarriageSystem` leva consigo os filhos pequenos que ficariam sem adulto (o casamento do único adulto os abandonava).
- **Pool agregado**: `Dematerialize` recusa criança em desenvolvimento. O resample de desenvolvimento segue **descartado**: sem criança no pool não há consumidor.

## Alternativas consideradas
- Adoção só por parentes — deixa órfãos sem parente morrerem.
- Exposição por ações de rotina (fala dirigida, ensino) — exigiria um medidor novo alimentado à mão.

## Consequências
- **Positivas**: o mundo deixa de matar órfãos por falta de dono; cuidador adotivo passa a moldar idioma e criação.
- **Negativas / trade-offs**: `Upbringing` deixa de ser imutável (o formato do snapshot não muda); o acolhimento escolhe por regra simples, sem
  disputa nem consentimento; a graduação é por número de cuidadores, não por qualidade.
- **Follow-ups**: adoção formal com herança e direitos (Fase 7); resample no pool se criança puder entrar nele.
