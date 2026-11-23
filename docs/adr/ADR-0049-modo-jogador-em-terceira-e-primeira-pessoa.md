# ADR-0049: modo jogador em 3ª/1ª pessoa com controle direto (emenda o ADR-0047)

- **Status**: proposto (a decisão de produto é do dono, 2026-11-23; este registro traz consequências e contrato)
- **Data**: 2026-11-23
- **Decisores**: dono do projeto (decisão); rascunho por agente (AD-196)

## Contexto
O ADR-0047 decidiu "manter o Godot 3D isométrico" para todo o cliente. Em 2026-11-23 o dono decidiu, para a camada do
jogador: "a câmera não será isométrica, será 3ª ou 1ª pessoa; usado como um jogo de RPG mesmo; o jogador anda no WASD ou
setas, pode interagir com as coisas, bater com clique". Hoje o jogador só escolhe ações de uma lista
(`/players/{playerId}/action`) e o motor anda em ticks de 1 hora numa grade de células; a única rota de passo
(`/visual/player/{id}/move`) move qualquer NPC sem controlador nem registro. Desenho: `.specs/features/player-direct-control/`.

## Decisão
1. **Emenda ao ADR-0047, ponto "manter 3D isométrico"**: vale para espectador, admin e mapa. Com personagem encarnado o
   cliente usa câmera em perspectiva de **3ª pessoa** (padrão) ou **1ª pessoa** (alternada por tecla). Sair do personagem
   volta à isométrica.
2. **Controle direto, autoridade do motor.** O cliente envia intenções (vetor de movimento por quadro, interagir com um
   alvo, golpear um alvo) com número de sequência; o motor valida (colisão, alcance, velocidade do corpo, carga, cansaço,
   ferimento) e aplica num passo fino dentro do tick corrente, só para encarnados. O cliente prevê e reconcilia; nunca
   manda posição.
3. **Mesmos sistemas de NPC.** Interagir resolve para a ação de NPC já existente com o alvo fixado; golpe é
   `Violence.Fight` com justiça, testemunha e boato de sempre. Nada de regra "de jogador".
4. **Registro reproduzível.** As intenções entram no `PlayerSessionLog` (AD-196) com tick e subtick; snapshot + registro
   reproduz o hash.

## Contrato (só acréscimos)
- `POST /players/{playerId}/input` (novo): lote de intenções `{seq, subtick, move:{x,y,run}, interact?:{kind,id},
  strike?:{npcId}}`; resposta com a pose autoritativa `{seq, x, y, floor, facing, speed, stamina}` e recusas por item.
- `GET /players/{playerId}/reach` (novo): o que o corpo alcança agora e as ações de NPC disponíveis em cada alvo.
- Campo novo na projeção do NPC encarnado: `pose` (posição contínua, andar, direção); omitido para os demais.
- Canal realtime do jogador com a pose a cada passo aplicado (acréscimo ao `RealtimeGateway`).
- `POST /visual/player/{id}/move`: passa a exigir controlador do NPC (ou é descontinuada; decisão do dono).
- Com a autenticação do ADR-0048, o `playerId` sai da sessão e as rotas novas nascem já com `401/403`.

## Alternativas consideradas
- **Manter isométrica com clique-para-andar** — rejeitada pelo dono.
- **Posição decidida no cliente e aceita pelo motor** — rápida, mas burla colisão, velocidade e o replay; o cliente
  passaria a escrever o mundo. Rejeitada.
- **Encolher o tick do mundo inteiro para segundos** — custo por NPC-tick multiplicado por milhares; rejeitada. O passo fino
  só do jogador mantém o resto do mundo em 1 h.

## Consequências
- **Positivas**: o jogador age como pessoa no mundo (andar, mexer, bater) sem sair das regras de NPC; o replay segue
  determinístico; espectador e admin não mudam.
- **Negativas / trade-offs**: duas rotas e um campo novos; câmera e entrada novas no cliente (personagem precisa de
  animação de andar, golpe e interação; a cidade vista de perto precisa de escala e detalhe que a isométrica escondia);
  passo fino é estado canônico enquanto há jogador; latência de rede exige predição.
- **Follow-ups**: AD-197 (fatia de implementação), animações e colisão de interior no cliente, teto de jogadores por mundo
  (ADR-0048), medida de FPS em 3ª pessoa como novo piso de desempenho (`docs/desempenho.md`).
