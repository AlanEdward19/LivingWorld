# ADR-0007: História como relato degradável, não como log comprimido

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O ADR-0006 previa "política de retenção" para o event log. Ao dimensionar, a retenção
genérica não fecha: uma cidade de 12 mil habitantes gera ~38 GB em 100 anos, e um mundo de
1 milhão por 2.000 anos passa de 58 TB. Comprimir o log resolveria o disco e destruiria o
produto — o que sobra de uma compressão fiel é um resumo sem graça.

O insight que resolve os dois problemas de uma vez: **história real também não é fiel**.
Existem livros de história, mas ninguém sabe se são verdade — são uma imagem construída
sobre uma escrita, com as alucinações de quem escreveu. A perda de informação não é a
dívida do sistema. É o assunto dele.

## Decisão
Vamos modelar o passado em camadas de fidelidade decrescente — fato, memória viva, relato,
tradição, registro escrito, mito — e fazer o NPC agir sobre a **crença**, nunca sobre o
fato. Concretamente:

- O motor guarda um **esqueleto imutável** do fato (quem, quando, tipo, participantes), que
  é pequeno e permanente. É a verdade, e só o motor a vê.
- Enquanto há testemunha viva, existe memória de alta fidelidade. Morta a última
  testemunha, o fato passa a existir apenas como **relatos** transmitidos.
- Cada salto de transmissão aplica **operadores de distorção determinísticos e semeados**
  (troca de atribuição, inflação de magnitude, compressão temporal, perda de causa,
  moralização, anacronismo, omissão conveniente, fusão de personagens).
- Cada meio (oral, canção, livro, monumento, registro oficial) tem fidelidade e alcance
  próprios. Livro congela o viés do autor; cópia introduz erro; livros se perdem e são
  redescobertos.
- Cada comunidade mantém um **cânone limitado** de N relatos vivos, com despejo por peso.
- A API expõe **duas consultas separadas**: verdade (motor/debug/autor) e crença (o que
  este NPC ou esta cultura acredita). Nenhum caminho de jogo alcança a verdade.
- A LLM **narra** o relato que o motor já distorceu. Ela nunca escolhe a distorção.

## Alternativas consideradas
- **Retenção genérica do ADR-0006** (comprimir eventos antigos por importância) — resolve
  disco, entrega um sumário fiel e sem vida, e não gera nenhum comportamento novo.
- **Guardar tudo** — 58 TB no cenário alvo. Inviável, e ainda daria a todo NPC uma
  onisciência histórica que contradiz o princípio de conhecimento limitado.
- **Distorção gerada pela LLM** — mais barato de escrever e destrói o determinismo: a
  mesma seed deixaria de reproduzir o mesmo mundo, e a LLM passaria a criar fato.

## Consequências
- **Positivas**: custo de armazenamento fica **independente do tempo** (cânone limitado) —
  1M de habitantes por 2.000 anos cabe em ~6 GB, ou ~130 MB amostrando esqueletos.
  Desbloqueia comportamento que nenhum outro sistema dava: cidades com versões
  incompatíveis da mesma guerra, o estudioso que acha um livro e contradiz o consenso,
  rixas familiares cuja origem ninguém lembra, heróis que nunca existiram.
- **Negativas / trade-offs**: dois modelos de leitura para manter coerentes (verdade e
  crença) e o risco permanente de vazar a verdade para o mundo do jogo; a distorção precisa
  ser determinística, o que a torna mais trabalhosa que sortear texto; depurar "por que o
  NPC acredita nisso?" exige ferramenta de proveniência desde o começo.
- **Follow-ups**: substitui a política de retenção genérica do ADR-0006 — este ADR não o
  revoga, especializa a parte de retenção. A Fase 9 é reescrita sobre este modelo, e o log
  em dois tiers desce para a Fase 3 para que o formato não congele errado.
