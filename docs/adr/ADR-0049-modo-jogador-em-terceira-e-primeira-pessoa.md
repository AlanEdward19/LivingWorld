# ADR-0049: modo jogador em 3ª/1ª pessoa com controle direto (emenda o ADR-0047)

- **Status**: proposto (a decisão de produto é do dono, 2026-11-23; este registro traz consequências e contrato)
- **Data**: 2026-11-23 (reescrito na AD-197a: mundo em tempo real, ADR-0050)
- **Decisores**: dono do projeto (decisão); rascunho por agente

## Contexto
O ADR-0047 decidiu "manter o Godot 3D isométrico" para todo o cliente. Em 2026-11-23 o dono decidiu, para a camada do
jogador: "a câmera não será isométrica, será 3ª ou 1ª pessoa; usado como um jogo de RPG mesmo; o jogador anda no WASD ou
setas, pode interagir com as coisas, bater com clique"; e, no mesmo dia, que com jogador o mundo inteiro roda em tempo real
(ADR-0050), sem "passo fino só do jogador". A identidade e o canal persistente do jogador são do ADR-0048 (aceito). Desenho:
`.specs/features/player-direct-control/design.md`.

## Decisão
1. **Emenda ao ADR-0047, ponto "manter 3D isométrico"**: vale para espectador, admin e mapa. Com personagem encarnado o
   cliente usa câmera em perspectiva de **3ª pessoa** (padrão) ou **1ª pessoa** (alternada por tecla). Sair do personagem
   volta à isométrica.
2. **Controle direto, autoridade do motor, no relógio do mundo.** Com jogador presente o mundo anda em tempo real (Δ fino do
   ADR-0050) para todos; o cliente envia intenções pelo canal persistente (vetor de movimento, interagir com um alvo, golpear
   um alvo) com número de sequência; o motor valida (colisão, alcance, velocidade do corpo, carga, cansaço, ferimento) e
   aplica no tick fino seguinte. O cliente prevê e reconcilia; nunca manda posição.
3. **Mesmos sistemas de NPC.** Interagir resolve para a ação de NPC já existente com o alvo fixado; golpe é `Violence.Fight`
   com justiça, testemunha e boato de sempre. NPCs andam e agem no mesmo tempo fino (pose pela rota e pelo instante de
   chegada; quem é tocado desperta na hora). Nada de regra "de jogador".
4. **Registro reproduzível.** Intenções, trocas de Δ e acelerações entram no `PlayerSessionLog` (ADR-0048) com o instante de
   mundo; snapshot + registro reproduz o hash.

## Contrato (só acréscimos)
- Comandos novos no canal `/players/ws` (ADR-0048): `move {seq, x, y, run}`, `interact {seq, kind, id}`, `strike {seq, npcId}`,
  `reach {seq}`; resposta com a pose autoritativa `{seq, x, y, floor, facing, speed, stamina}` ou a recusa com motivo.
- Rota HTTP equivalente `POST /players/{playerId}/input` (lote) e `GET /players/{playerId}/reach`, para quem não abre o canal.
- Campo `pose` (posição contínua, andar, direção) na projeção dos NPCs no escopo do jogador.
- Comandos de tempo do jogador sozinho (ADR-0050): `idle-fast`, `sleep-until {hour}`, `sleep-fast`, recusados com outro
  jogador conectado.
- `POST /visual/player/{id}/move`: fechar ou exigir controlador continua decisão do dono (não aprovado).

## Alternativas consideradas
- **Manter isométrica com clique-para-andar** — rejeitada pelo dono.
- **Posição decidida no cliente** — burla colisão, velocidade e o replay. Rejeitada.
- **Passo fino só do jogador dentro do tick de 1 h** — rejeitada pelo dono (o mundo tem de estar em tempo real junto).

## Consequências
- **Positivas**: o jogador age como pessoa no mundo, no mesmo tempo dos NPCs, sem sair das regras de NPC; o replay segue
  determinístico; espectador e admin não mudam.
- **Negativas / trade-offs**: depende da migração do tempo do ADR-0050; comandos e campo novos; câmera, entrada, animação de
  andar, golpe e interação no cliente; a cidade vista de perto pede escala e detalhe que a isométrica escondia.
- **Follow-ups**: AD-197b/c (tempo, ADR-0050), AD-197d (controle direto), colisão de interior, piso de FPS em 3ª pessoa
  (`docs/desempenho.md`).
