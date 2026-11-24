# ADR-0048: identidade do jogador e ordem única de aplicação no multiplayer

- **Status**: aceito (dono, 2026-11-23: "eu aprovo"; ressalva: "minha preocupação é latência nos comandos, poderíamos manter
  conexão após conectado (websocket talvez), precisamos que não tenha demora para interagir"). Implementado na AD-197a.
- **Data**: 2026-11-23
- **Decisores**: dono do projeto

## Contexto
As rotas `/players/*` (AD-111) aceitam qualquer `playerId` no corpo ou no caminho: qualquer cliente pode agir por outro
jogador. A Fase 25 pede vários jogadores no mesmo branch com uma ordem de aplicação reproduzível a partir do snapshot
(ADR-0008, ADR-0015). A AD-196 já entregou a parte que não é contrato: o `PlayerSessionLog` do hospedeiro guarda cada comando
aceito com o tick do mundo e a ordem de chegada, e `PlayerReplay` reaplica o registro sobre o snapshot e reproduz o hash
(`PlayerParityTests.Interleaved_actions_of_two_players_replayed_from_the_snapshot_reproduce_the_hash`). Falta decidir de onde
vem a identidade e como ela entra no contrato público.

## Decisão (recomendada)
1. **Identidade vem da sessão, nunca do corpo.** O hospedeiro emite um token de sessão opaco (`POST /sessions`, corpo vazio,
   devolve `{sessionToken, playerId}`); o `playerId` é gerado pelo hospedeiro e amarrado ao token. As rotas de jogador passam a
   ler o jogador do cabeçalho `Authorization: Bearer <token>`. As rotas antigas com `playerId` no caminho ficam aceitas só sem
   autenticação ligada (modo local, o de hoje) e são descontinuadas depois de uma fase.
2. **Sem provedor externo nesta fase.** Token aleatório do hospedeiro (fora de `LivingWorld.Simulation`, sem efeito no mundo)
   guardado em memória com validade; OAuth/OIDC fica para quando houver servidor público (ADR futuro).
3. **Uma fila por branch.** Todo comando de jogador entra numa fila única do branch e é aplicado no limite entre ticks, na
   ordem de chegada (`Sequence`), pelo mesmo laço que roda o relógio. O registro é persistido junto com o snapshot periódico
   (arquivo `players.log` ao lado do save), para que reaplicar snapshot + registro dê o mesmo mundo depois de reiniciar.
4. **Estado do hospedeiro, fora do hash.** Sessão, token e fila não entram no snapshot canônico; só o efeito no mundo
   (controlador, ação pedida) entra, como hoje.
5. **Modo RPG (ADR-0049)**: as intenções de movimento, interação e golpe entram na mesma fila, com subtick; a ordem entre
   jogadores no mesmo subtick é a de chegada (`Sequence`).
6. **Respostas novas** (contrato, por isso este ADR): `401` sem token válido e `403` quando o token não é do jogador da rota.

## Alternativas consideradas
- **Manter o `playerId` do cliente e confiar nele** — é o de hoje; qualquer um age por qualquer um. Serve só a um jogador local.
- **OAuth/OIDC já agora** — dependência nova e infraestrutura de servidor público que o projeto ainda não tem.
- **Aplicar o comando na hora em que chega (fora do limite de tick)** — a ordem dependeria da concorrência das requisições;
  o replay não reproduziria. Rejeitado.
- **Um scheduler por jogador** — cada um com seu tempo quebra "um único scheduler por branch" da Fase 25.

## Consequências
- **Positivas**: ninguém age por outro; vários jogadores no mesmo branch têm ordem única e reproduzível; o replay já provado
  passa a valer também depois de reiniciar o hospedeiro.
- **Negativas / trade-offs**: duas rotas novas (`/sessions`) e dois status novos nas rotas de jogador; o cliente Godot troca o
  `playerId` local (`user://player.cfg`) pelo token; a fila acrescenta até um tick de latência entre pedir e valer.
- **Follow-ups**: provedor de identidade externo quando houver servidor público; teto de jogadores por mundo (questão aberta
  da Fase 25: dois jogadores em pontas opostas dobram o custo materializado); presença entre branches continua proibida
  (ADR-0008).

## Implementação (AD-197a)
- `POST /sessions`, token opaco (`PlayerSessionTokens`, 12 h ociosas), `Authorization: Bearer`, 401/403; `PLAYER_AUTH=required`
  desliga o modo local. Fila única = a trava do laço do relógio (`PlayerCommandGateway`), aplicada entre ticks na ordem de
  chegada, sem esperar o próximo tick (latência). `{mundo}.players.base.json` + `{mundo}.players.log` ao lado do save.
- **Canal persistente** (ressalva de latência): `GET /players/ws` com o mesmo token; comandos numerados e resposta pelo mesmo
  canal; as rotas HTTP seguem valendo. Pausa do admin = 409 nas rotas e no canal (aprovado pelo dono em 2026-11-23).
