# ADR-0029: Exposição binária, janela crítica por teto e aquisição sem sorteio (Fase 21b)

- **Status**: aceito
- **Data**: 2026-10-14
- **Decisores**: Alan (pediu a 21b, issue #71; follow-up já previsto no ADR-0028)

## Contexto
A 21b troca o progresso "por idade" da 21a por aquisição com exposição e janela crítica. O plano previa rolagem do `Resolver`
(perfil Agregado) por marco e exposição graduada (fala dirigida, brincadeira, ensino). Duas restrições pesam: o critério de gene exige
dois NPCs com genes e exposição idênticos terminarem **byte-idênticos**, e as ações de rotina que dariam graus de exposição não existem.

## Decisão
- **Sem sorteio**: a variação individual é o `RateGene` (idade efetiva = idade × taxa). Rolagem por NPC impediria o critério byte-idêntico.
  O `Resolver` volta se uma fase futura precisar de incerteza que o gene não cobre.
- **Exposição binária**: no mês, a criança tem exposição se convive com ao menos um adulto vivo do próprio domicílio. Sem domicílio ou
  sem adulto, nenhuma. Graus de exposição ficam para quando existirem as ações que os produzem.
- **Dinâmica por eixo** (`OntogenyRules.Advance`): com exposição o progresso ganha o que a curva da idade andou mais uma fração
  (`LateRecoveryFactor`) do atraso, até o teto; sem exposição não ganha. Quando uma janela fecha com o marco fora do alcance, o teto
  do eixo cai para `progresso + (1 − progresso) × MissedWindowKeep`, **para sempre**. Os dois parâmetros são dado de cenário.
- `DevelopmentState` ganha 6 tetos (default 1,0); quem passa da idade de conclusão com algum teto reduzido **mantém** o estado final
  (déficit permanente) em vez de virar `null`.

## Alternativas consideradas
- Rolagem do `Resolver` por marco — quebra o critério byte-idêntico e adiciona ruído sem consumidor.
- Exposição graduada — inventaria um medidor alimentado à mão, o que o plano proíbe.

## Consequências
- **Positivas**: causalidade limpa e testável (braços pareados diferem numa só coisa); custo mensal O(crianças).
- **Negativas / trade-offs**: adultos com déficit permanente seguem com `Development` não nulo; `Cry` ainda sem consumidor causal
  (o choro atendido já equivale à presença do cuidador). Os valores padrão de recuperação e perda são autorados, não medidos.
- **Follow-ups**: 21c (idioma do cuidador, regressão, pool agregado) muda o snapshot de novo.
