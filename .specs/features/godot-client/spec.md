# Cliente Godot — Specification

> **Spec, não gate. Sem implementação.** Decisão em ADR-0022 (Godot substitui o web). O cliente é só leitura e
> renderização: o motor (`LivingWorld.Api`) é dono do estado, do tempo e do determinismo.

## Problem Statement

O web saiu do repositório. Falta um cliente gráfico (cenas, câmera, sprites, animação) que mostre um mundo vivo
sem duplicar regra de simulação nem inventar contrato novo a cada tela. O contrato já existe e está congelado em
testes: `tests/contracts/openapi.v1.json` (rotas e tipos) e `tests/contracts/shape.*.json` (forma das respostas
visuais). Mudar a API muda esses arquivos no mesmo commit, e o diff diz o que o cliente precisa acompanhar.

## Fronteira (o que o cliente pode usar)

| Necessidade | Rota |
| --- | --- |
| Estado inicial e atualizações de um escopo (mundo, cidade, interior) | `GET /visual/subscribe`, `GET /visual/replay`, `GET /visual/sse`, `GET /visual/ws` |
| Controlar o relógio | `POST /simulation/pause`, `resume`, `speed`, `step`, `advance-year`; `GET /simulation/status` |
| Criar e iniciar mundo | `POST /worlds/preview`, `create`, `start`; `GET /periods`, `/periods/{id}/catalog` |
| Inspecionar pessoa, história, narrativa | `GET /npcs/{id}`, `/narratives/*`; `POST /npcs/{id}/watchlist`, `/observation/scope` |
| Conversar e autorar (modo criador) | `POST /conversations/*`, `/authoring/*` |
| Andar como jogador | `POST /visual/player/{id}/move` |

O cliente não calcula necessidade, economia nem decisão. Tudo que aparece na tela vem de um delta com
cursor (`tick`, `sequence`, `scopeKey`); lacuna ou duplicata de sequência é tratada com `replay`, nunca por palpite.

## Goals

- [ ] Projeto Godot (C#) em `client/godot/`, fora do gate .NET; abre sem tocar em `src/`.
- [ ] **Cliente de contrato** isolado (uma camada): HTTP + SSE/WS, desserialização tipada, cursor e reconexão com
      `replay`. Cenas e UI só dependem dessa camada.
- [ ] Mapa do mundo e cidade a partir das camadas visuais (`VisualLayerId`); NPCs com ação, rota de viagem e
      estado vivo (necessidades, saúde) como o servidor projeta.
- [ ] Acessibilidade e legibilidade: toda dica visual de ação tem equivalente em texto (rótulo do catálogo de
      apresentação do servidor, não literal no cliente).
- [ ] Tempo real sem perder o determinismo: o cliente nunca avança o mundo sozinho, só pede ao servidor.

## Out of Scope

| Item | Razão |
| --- | --- |
| Regra de simulação no cliente | O motor é a fonte da verdade (ADR-0005, ADR-0014) |
| Arte final, som, UI de menus | Primeiro cliente é funcional; arte vem depois |
| Multiplayer e autenticação | Pausa e auditoria de admin já são decisão do servidor (ADR-0015) |
| Veículos e estradas emergentes | Specs próprias (`emergent-vehicles`) |

## Critérios de verificação (R1–R5 de `rules/eval-criteria.md`)

- **Contrato congelado**: `ApiContractTests` falha se OpenAPI ou forma das respostas mudarem sem regravar; o
  cliente de contrato do Godot é gerado/validado contra `tests/contracts/openapi.v1.json` em CI.
- **Sem estado próprio**: apagar o cache do cliente e reconectar reconstrói a tela idêntica (replay a partir do cursor).
- **Lacuna de sequência**: teste do cliente injeta delta fora de ordem e confere que dispara `replay`.
- **Só leitura**: rodar o cliente 1 h simulada contra um servidor de teste não altera o hash canônico do mundo
  (exceto pelos comandos explícitos de relógio/autoria).
- **Texto equivalente**: toda ação exibida tem rótulo textual vindo do servidor.

## Questões em aberto
- C# do Godot referenciando `LivingWorld.Domain` direto (tipos compartilhados) ou só DTOs gerados do OpenAPI?
- SSE ou WebSocket como canal padrão (o servidor oferece os dois)?
- Onde vive o CI do cliente (Godot headless no runner)?
