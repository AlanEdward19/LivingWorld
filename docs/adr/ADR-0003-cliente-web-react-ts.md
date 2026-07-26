# ADR-0003: Cliente web em React + TypeScript, separado da API

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O roadmap entrega um mapa 2D navegável (mundo → região → cidade → bairro → local → NPC),
com camadas (terreno, biomas, rotas, fronteiras, conflitos) e milhares de entidades
desenhadas ao mesmo tempo. Isso é renderização em canvas/WebGL, não formulário.

Quando este ADR foi escrito, o mapa era a Fase 2. Depois (AD-007) a Fase 2 virou geografia
só de dados e o cliente visual foi para a **Fase 14**, fora do caminho crítico do objetivo
#1. A escolha de tecnologia abaixo não muda — só o momento em que ela é exercida.

## Decisão
Vamos usar **React + TypeScript** em `LivingWorld.Web`, consumindo a API REST/realtime
como qualquer outro cliente. O front é um cliente entre outros — Unreal virá depois pela
mesma API, e a API não pode assumir nada sobre quem a consome.

Contratos: os tipos TypeScript são **gerados** a partir do schema OpenAPI da API, não
escritos à mão. Contrato duplicado à mão diverge.

## Alternativas consideradas
- **Blazor** — um só idioma e reuso direto dos tipos do `Domain`, mas o ecossistema de
  renderização 2D em larga escala (pixi, deck.gl, canvas tooling) é bem mais fraco, e o
  mapa é justamente a parte visualmente pesada do produto.
- **Só API + CLI** — descartado como escolha *permanente*, mas adotado como ordem: o
  objetivo #2 (inspecionar qualquer NPC vivo) fecha por CLI/API na Fase 8, sem front.

## Consequências
- **Positivas**: melhor ferramental para o mapa; a API fica honestamente agnóstica de
  cliente desde o começo, o que é pré-requisito para o cliente Unreal.
- **Negativas / trade-offs**: segundo toolchain (Node/npm) no repo; `scripts/build.sh` e
  `verify.sh` passam a ter um ramo web; risco de divergência de contrato, mitigado pela
  geração a partir do OpenAPI.
- **Follow-ups**: na Fase 14, estender `scripts/*.sh` para o ramo web e adicionar a
  geração de tipos ao gate (`git diff --exit-code` sobre o diretório gerado).
