# Controle direto de RPG (3ª/1ª pessoa, WASD, interagir, golpe) [Estado: AD-197d, 197j e 197k entregues (ADR-0049 e ADR-0051 aceitos)]

Decisões do dono (2026-11-23): câmera em 3ª/1ª pessoa, WASD ou setas, interagir, bater com clique; com jogador o mundo inteiro
roda em tempo real (proporção configurável por mundo, padrão 1:1); avançar horas só o admin ou o jogador sozinho. Regra do
projeto: tudo real no motor. ADRs: 0048 (sessão e canal, aceito), 0049 (câmera e controle), 0050 (tempo real, aceito; contrato em versão nova).

## O que existe hoje
- **Agir**: `/players/*` (incarnate, action com Eat, Sleep, Work, Socialize, Idle, Buy, UsePower+powerId, logout), sessão por
  token (`POST /sessions`) e canal persistente `/players/ws` com comandos numerados (AD-197a). Pausa do admin = 409.
- **Passo (AD-197e)**: comando `move {dx, dy}` (intenção, não posição) no canal e em `POST /players/{playerId}/move`, só no corpo
  que a sessão controla, pela fila (pausa 409, registro). A rota `/visual/player/{id}/move` saiu (decisão do dono). Golpe
  `strike {npcId}` já pelo `Violence.Fight`. Posse comum e plena (AD-197e): ver item 9.
- **Tempo e espaço**: 1 tick = 1 h; grade de células inteiras; deslocamento por rota com duração (`Mobility.TravelTicks`) e o
  cliente interpola por `routeArrivalTick`; interior por prédio, andar e célula local. Necessidades preguiçosas por tempo.
- **Interação de NPC**: comer, beber, comprar, trabalhar, conversar, dormir, buscar água, levar carga, recolher espólio,
  furtar, usar poder. **Agressão**: `Violence.Fight` + `Justice.OnCrime` (testemunha, boato, veredito, castigo), mágoa,
  vingança, psique; combate extraordinário.
- **Cliente**: HUD do jogador (ações, poderes, sair, pausa), `npc_actor.gd`, câmera ortográfica isométrica.

## Desenho (sobre o relógio do ADR-0050)
1. **Um relógio só.** Com jogador presente o mundo anda em Δ fino (ex.: 50 ms de mundo por tick a 1:1) para todos. Sem jogador,
   o ritmo do mundo é o configurado (padrão aprovado: rápido, Δ = 1 h, no ritmo do admin). O jogador sozinho pode acelerar ocioso ou ao dormir (até um
   horário ou acelerando; evento que acorda interrompe); com dois ou mais jogadores ninguém pula o tempo.
2. **Movimento como entrada validada.** WASD/setas viram intenção (direção, andar/correr) enviada pelo canal com `seq`. O motor
   aplica no tick seguinte: posição contínua (`pose`, canônica enquanto encarnado), velocidade pela conta de `Mobility` e do
   corpo (músculo, carga, ferimento, cansaço, terreno, rua, tempo), colisão com prédio, parede, água e ocupantes, escada troca
   de andar; cansaço e sono gastam pelo tempo andado. O registro guarda a intenção e o Δ, não a posição.
3. **NPC no mesmo tempo.** Decisão por despertar (em segundos); pose entre células calculada pela rota e pelo instante de
   chegada; quem é tocado pelo jogador desperta pelo evento. Nada de subtick para NPC parado.
4. **Interagir** (E/clique direito): `reach` lista o que o corpo alcança e as ações de NPC possíveis em cada alvo (comprar,
   comer, beber, conversar, dormir, trabalhar, pegar/largar carga, abrir porta). O que falta (porta, objeto avulso) entra
   para NPC e jogador juntos.
5. **Golpe no clique** = `Violence.Fight` com o alvo sob a mira, ao alcance (1 célula ou o da arma), cadência pelo corpo, força
   pelo músculo, arma na mão quando houver; ferimento, morte, testemunha, boato, denúncia, justiça, mágoa e vingança de sempre.
6. **Câmera**: 3ª pessoa por padrão, 1ª por tecla, só encarnado; espectador e admin na isométrica.
7. **Latência**: o canal persistente leva entrada e pose; o cliente prevê o próprio passo com os mesmos dados do motor e
   reconcilia pela pose autoritativa (por `seq`); divergência grande reposiciona. Medida do canal na AD-197a.
8. **Custo**: a 1:1 o mundo faz uma hora de sistemas por hora real; o tick fino paga despertares vencidos, entradas e o passo
   do jogador. Meta: ≤1,02× por NPC-tick com Δ = 1 h; com jogador, o tick fino dentro do orçamento de quadro do servidor; FPS
   do cliente sem regressão no `bench.sh`.

## Entregue na AD-197d (ADR-0051)
- **Câmera**: 3ª pessoa ao encarnar, C troca para a 1ª, mouse olha; sair volta à isométrica (`camera_rig.gd`).
- **Passo**: WASD relativo ao olhar → `move {dx, dy}`; o motor valida o chão (`PlayerGround`: parede pela planta na grade do mundo,
  porta pela soleira, gente pela pose, canto de parede) e toma o rumo (`TakeTheWheel`); o cliente prevê e corrige.
- **Interagir**: `reach` (oferta do motor) e `interact {kind, id, verb, resourceId?, quantity?}` (`PlayerInteractions`): porta,
  despensa, banca, cama, pessoa (conversar, bater, abandonar o filho), casa (vender). Ordens contra o que a pessoa valoriza:
  `PlayerOrders` pela disputa do AD-197f. Golpe no clique pelo `strike`.
- **AD-197j**: passo no tempo do corpo (`PlayerCommands.PaceMs` = `Mobility.TravelMs` × corpo × carga × chão, mais lento cansado;
  antes do fim, recusa; gasta sono); escada e andares de cima pelo `Interior`; fonte (beber, encher), lavoura (trabalhar),
  espólio (recolher) e furto de despensa, banca, lavoura e espólio alheios pelo `Theft`; cliente com golpe animado, Z prende o
  cursor, mira no centro, nomes de recurso. Abandono sem outro povoado deixa sem teto (`UnionDissolution`).
- **AD-197k**: passo solto (`walk {dx, dy}` em milésimos; `PlayerWalk`, `Npc.Fine` em milésimos de célula): velocidade em m/s pela
  mesma conta (`PaceMs` relativo ao chão mais fácil × 1,4 m/s, célula de 1,5 m); a célula, a parede, a porta e a gente seguem da
  grade. Cursor preso no modo jogador (Z solta e prende; menu, conversa e Esc soltam). Mobília pelo catálogo de objetos e oficina
  sem preço na oferta. Escala em aberto: a viagem dos NPCs conta ~3 min por célula, o passo solto 1,5 m por célula (ADR-0051).

## Critérios (a escrever ao ativar, `rules/eval-criteria.md`)
- Mesmo registro (entradas, Δ, acelerações) reaplicado do snapshot dá o mesmo hash; entrada contra parede não move.
- Velocidade cai com carga, ferimento e cansaço (par na mesma seed, direção em ≥18/20).
- Golpe do jogador e `Fight` do NPC com o mesmo par e seed: mesmo dano, mesmo caso, mesmo hash.
- Com dois jogadores conectados, todo comando de acelerar é recusado e o hash não muda.

9. **Posse** (AD-197e, mesma base para jogador e possessão futura): `PossessionMode` no corpo. Comum: `PossessionWill` recusa
   ferir quem a pessoa valoriza muito (vínculo × lealdade ≥ `RefuseHarmCare`), sem sorteio; a recusa volta com o motivo, não
   muda o mundo e fica no registro. Plena (admin, `POST /players/{npcId}/possess`): obedece; a pessoa presa (`CaptiveMind`)
   acumula objeções e sofrimento e, solta, vive a posse na psique. `GET /players/{playerId}/captive` mostra ao possuidor o que
   ela sente e quer. Falar com ela e ouvir o pensamento no chat passa pela LLM, que só põe em palavras o `CaptiveView`.

## Decisões do dono (2026-11-23)
1. Posse: pelo admin, sem recusa (a pessoa fica presa e pode ser ouvida); sem admin, recusa o que fere muito o que ela valoriza.
2. `/visual/player/{id}/move` removida; o passo é o `move` (feito).
3. Tecla da 1ª pessoa (C) e padrão 3ª pessoa: feito na AD-197d, aceite pendente no ADR-0051.
4. O sono acelerado é interrompido por golpe, fogo, fome/sede crítica e alguém chamando (AD-197c).
