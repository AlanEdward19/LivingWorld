# ADR-0011: Um primitivo único de resolução, com variância declarada

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O usuário quer o toque de RPG de mesa: por mais que o personagem tenha atributos, existe
sempre o d20. Isso não é enfeite — é o mesmo princípio que já governa o projeto ("tudo
calculado, nada garantido") e vale para tudo: cortejo, combate, aprendizado, uso de poder,
resistência a doença, salto temporal, colheita, negociação.

Sem uma decisão, cada sistema inventa a própria forma de sortear. Aí ninguém consegue
balancear nada, o determinismo fica difícil de auditar e "nada garantido" vira acidente em
vez de regra.

Há uma tensão real: o d20 tem 5% de crítico em cada ponta. Ótimo para um duelo, terrível
para 12 mil colheitas por ano — variância dramática aplicada em massa vira ruído branco que
apaga a causalidade que a simulação está tentando produzir.

## Decisão
Vamos ter **um primitivo de resolução** usado por todo sistema que decide algo incerto:

```
Resolver(dificuldade, modificadores, perfilDeVariância, rng) -> Resultado
Resultado: falhaCrítica | falha | sucessoParcial | sucesso | sucessoCrítico
```

- **Modificadores** vêm de atributos, habilidade, personalidade, humor, cultura, contexto e
  potência. É onde a competência entra.
- **Perfil de variância** é declarado por domínio, não fixo:
  - `Dramático` (d20): indivíduo, cena, confronto, cortejo, uso de poder. Críticos existem.
  - `Agregado` (curva estreita): produção, preço, demografia, tráfego. Sem crítico.
  - `Raro` (cauda longa): mutação, invenção, contato, catástrofe.
- **Sucesso parcial** é resultado de primeira classe. É ele que gera história: você
  conseguiu, mas alguém viu.
- Sorteio usa o RNG semeado por stream (ADR-0005). Nada de `Random` solto.
- Todo sistema que decide algo incerto **usa este primitivo** — teste de arquitetura reprova
  quem sortear por fora.

## Alternativas consideradas
- **d20 puro em tudo** — máximo de sabor de mesa e ruído demais no agregado: 5% das
  colheitas viram desastre crítico todo ano, em toda fazenda, para sempre.
- **Cada sistema com seu sorteio** — flexível e improvável de balancear; auditar
  determinismo passa a exigir ler cada sistema.
- **Só curvas contínuas, sem crítico** — estatisticamente mais limpo e sem o momento de
  mesa que o usuário pediu explicitamente.

## Consequências
- **Positivas**: balanceamento passa a ter um lugar só; o sabor de d20 aparece onde importa
  (cena, indivíduo) sem contaminar a demografia; sucesso parcial gera intriga de graça;
  auditar determinismo é auditar um primitivo.
- **Negativas / trade-offs**: escolher o perfil errado num sistema é um bug sutil e difícil
  de ver — só aparece como "o mundo está estranho" muitas horas depois; e um primitivo
  central é ponto de contenção quando alguém quiser uma mecânica que não cabe nos perfis.
- **Follow-ups**: o primitivo nasce na Fase 0 junto de `Money` e `Result<T>`, porque toda
  fase seguinte depende dele. Perfis novos são dado de cenário, não código.
