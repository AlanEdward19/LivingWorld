# Controle direto de RPG (3ª/1ª pessoa, WASD, interagir, golpe) [Estado: desenho para a AD-197, nada implementado]

Decisão do dono (2026-11-23): "a câmera não será isométrica, será 3ª ou 1ª pessoa; o jogador anda no WASD ou setas, pode
interagir com as coisas, bater com clique". Regra do projeto: tudo real no motor, nada só visual. ADR: rascunho ADR-0049.

## O que existe hoje (levantamento da AD-196)
- **Agir**: `/players/{npcId}/incarnate`, `/players/{playerId}/action` (Eat, Sleep, Work, Socialize, Idle, Buy, UsePower+powerId),
  `/players/{playerId}/logout`. A ação pedida troca só a escolha; percepção, passos, rota e resolução são as de qualquer NPC.
- **Lacuna grave**: `POST /visual/player/{id}/move` move **qualquer** NPC uma célula (só checa vivo, mapa, distância 1): sem
  controlador, sem pausa, sem log, sem custo. Escrita fora do `PlayerCommands`; a AD-197 deve substituí-la ou fechá-la.
- **Tempo**: 1 tick = 1 hora de mundo. **Espaço**: grade de células inteiras (`CellCoord`); deslocamento é rota com duração
  (`Mobility.TravelTicks`: custo do mapa × corpo/poder × tecnologia × tempo) e o cliente interpola o boneco por `route` e
  `routeArrivalTick`. Interior por prédio, andar (`FloorLevel`) e célula local (`InteriorOccupancy`).
- **Interação de NPC já no motor**: comer/beber da despensa, comprar na banca, trabalhar, conversar (`Socialize`, companhia),
  dormir, buscar água, levar carga e recipiente (AD-147/168), recolher espólio, furtar (`TheftResolver`), usar poder.
- **Agressão**: `Violence.Fight` (dano pela força relativa de músculo, golpe mortal raro, ferimento pela saúde dinâmica
  AD-134, mágoa, vingança dos parentes, psique), `Justice.OnCrime` (testemunha acordada/dormindo, denúncia, boato com
  vida e teto de quem sabe, veredito, multa, castigo, banimento, forca), combate extraordinário (`CombatEncounterSystem`).
- **Cliente**: `player_hud.gd` (lista de ações, poderes, sair, aviso de pausa), câmera `camera_rig.gd` ortográfica
  isométrica com seguir; `npc_actor.gd` com corpo derivado do estado; nada de câmera em perspectiva nem entrada WASD.

## Desenho
1. **Duas escalas de tempo, um motor.** O mundo segue em ticks de 1 h. O modo jogador abre um **passo fino** só para a
   célula do jogador: o hospedeiro acumula entradas de movimento por quadro e o motor as aplica num subtick determinístico
   (`PlayerStep`, ex.: 1/60 h) dentro do tick corrente, sem andar o relógio do mundo. Recomendado: o mundo não acelera nem
   pausa por causa do jogador; a velocidade de admin continua global (ADR-0015).
2. **Movimento como entrada validada.** WASD/setas viram um vetor de intenção (direção, correr/andar) por subtick. O motor:
   posição contínua dentro da célula (`PlayerPose`, canônica enquanto encarnado), velocidade do corpo (a mesma conta de
   `Mobility`/corpo: músculo, carga, ferimento, cansaço, terreno, rua, tempo), colisão com prédio, parede de interior,
   água e quem ocupa a célula; troca de célula quando cruza a borda; escada muda de andar. Cansaço e sono gastam pelo
   tempo andado. O comando gravado é a intenção, não a posição: o replay (`PlayerSessionLog`) reaplica e dá o mesmo mundo.
3. **Interagir pelo que está ao alcance** (tecla E/clique direito): o motor lista o que o corpo alcança (pessoa, banca,
   despensa, porta, cama, fonte, espólio, objeto) e cada opção vira a ação de NPC já existente com o alvo fixado: comprar,
   comer, beber, conversar, dormir, trabalhar, pegar/largar carga, abrir porta. Sem ação nova "de jogador"; o que falta
   ao NPC (abrir porta, pegar objeto avulso) entra para NPC e jogador juntos.
4. **Golpe no clique** = `Violence.Fight` com o alvo sob a mira, se ao alcance (1 célula ou a da arma), com cadência pelo
   corpo; força pelo músculo, arma na mão quando houver objeto (AD-141) e as consequências de sempre: ferimento, morte,
   testemunha, boato, denúncia, justiça, mágoa, vingança. Golpe é crime como o do NPC; não há modo sem consequência.
5. **Câmera no cliente**: 3ª pessoa por padrão, 1ª por tecla (V ou F5), só com personagem encarnado; espectador e admin
   ficam na isométrica. O cliente desenha só o que o motor diz (pose, ação, ferimento).
6. **Latência e predição**: o cliente prevê o próprio passo com a mesma conta de velocidade e colisão (dados do motor) e
   corrige pela pose autoritativa a cada resposta (reconciliação com o número de sequência da entrada). Divergência
   grande teleporta o boneco para a pose do motor; o motor nunca aceita pose do cliente.
7. **Custo**: o passo fino só roda para encarnados (zero com o mundo sem jogador; o hash dos goldens não muda). Teto
   recomendado de entradas por segundo por jogador; o subtick resolve colisão só na vizinhança da célula. Meta: ≤1,02× por
   NPC-tick com um jogador andando a 1.000 NPCs; FPS do cliente sem regressão no `bench.sh`.

## Critérios (a escrever ao ativar, `rules/eval-criteria.md`)
- O mesmo registro de entradas reaplicado do snapshot dá o mesmo hash; entrada contra parede não muda a posição.
- Velocidade cai com carga, ferimento e cansaço (par na mesma seed, direção em ≥18/20).
- Golpe do jogador e `Fight` do NPC com o mesmo par e seed: mesmo dano, mesmo caso na justiça, mesmo hash.
- Sem personagem, nenhuma rota de movimento escreve; a rota antiga de `/visual/player/move` recusa NPC sem controlador.

## Decisões do dono (com recomendação)
1. Passo fino só para o jogador dentro do tick de 1 h (recomendado) ou o mundo inteiro desacelera quando há jogador?
2. Ao encarnar, o relógio do mundo continua no ritmo do admin (recomendado) ou cai para tempo real (1 h de mundo = 1 h)?
3. A ordem por clique respeita a vontade do NPC tomado (coautoria do desenho `player-take-a-life`) ou é sempre obedecida no
   modo RPG? Recomendo: movimento e golpe sempre obedecidos; recusa só em ação longa (trabalhar, dormir) e no limite do corpo.
4. Fechar a rota `/visual/player/{id}/move` (recomendado: substituir por `/players/{playerId}/input`) ou mantê-la para admin.
5. Tecla da 1ª pessoa e padrão 3ª pessoa (recomendado).
