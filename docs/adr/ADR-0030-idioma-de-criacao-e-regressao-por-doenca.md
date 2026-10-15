# ADR-0030: Idioma de criação, regressão por doença e adultos no pool (Fase 21c)

- **Status**: aceito
- **Data**: 2026-10-14
- **Decisores**: Alan (pediu a 21c, issue #72; follow-up previsto nos ADR-0028/0027)

## Contexto
A 21c fecha a Fase 21: a língua que a pessoa fala vem de quem a cria, não da etnia; doença regride o que ainda não se consolidou, com
causa datada; e a issue previa um resample de desenvolvimento no pool agregado. Verificado no código: o pool só guarda adultos
(a materialização sorteia idade ≥ 18 e só NPC vindo do pool volta a ele), então o resample não tem consumidor.

## Decisão
- **Idioma = cultura de quem fala.** `DevelopmentState.Fluency` reparte o ganho mensal de linguagem entre as culturas dos cuidadores
  (um voto por adulto do domicílio); ninguém nasce falando. Ao completar o desenvolvimento, o idioma dominante vira
  `Npc.NativeLanguage` (explícito no snapshot). `OntogenyMechanic.SpokenCulture` é a leitura única.
- **Consumidor causal**: a afinidade cultural do cortejo compara o idioma falado, não `Npc.Culture`. Em mundo de cultura uniforme nada muda.
- **Regressão por doença**: com infecção ativa, o progresso de cada eixo cai `RegressionLoss` por mês, sem passar do piso (maior marco
  atingido com a janela já fechada). Cada queda emite `WorldEventKind.MilestoneRegressed` no mesmo tick, com a doença no payload.
- **Pool agregado**: sem `MilestoneProgressSum` nem resample até uma criança poder entrar no pool; o invariante "pool só tem adultos" é testado.
- **Correção da 21b**: `IsComplete` com tolerância de ponto flutuante e o último passo mensal deixa de ser perdido ao cruzar a idade de conclusão.

## Alternativas consideradas
- Resample no pool agregado — código morto hoje, contra a regra de coesão.
- Trocar `Npc.Culture` pela cultura de criação — `Culture` é identidade imutável usada por história e tecnologia.

## Consequências
- **Positivas**: idioma e doença deixam rastro explicável; mundo de cultura uniforme mantém o comportamento.
- **Negativas / trade-offs**: `NativeLanguage` e `Fluency` aumentam o snapshot; valores de `RegressionLoss` são autorados.
- **Dívida**: cópia de crença/traço por cuidador adotivo (`Upbringing` é fixado no nascimento), resample no pool e consumidor próprio do `Cry`.
