# Controle direto de RPG (3ª/1ª pessoa, WASD, interagir, golpe) [Estado: desenho, nada implementado]

Decisões do dono (2026-11-23): câmera em 3ª/1ª pessoa, WASD ou setas, interagir, bater com clique; com jogador o mundo inteiro
roda em tempo real (proporção configurável por mundo, padrão 1:1); avançar horas só o admin ou o jogador sozinho. Regra do
projeto: tudo real no motor. ADRs: 0048 (sessão e canal, aceito), 0049 (câmera e controle), 0050 (tempo real).

## O que existe hoje
- **Agir**: `/players/*` (incarnate, action com Eat, Sleep, Work, Socialize, Idle, Buy, UsePower+powerId, logout), sessão por
  token (`POST /sessions`) e canal persistente `/players/ws` com comandos numerados (AD-197a). Pausa do admin = 409.
- **Lacuna**: `POST /visual/player/{id}/move` move qualquer NPC uma célula, sem controlador, pausa nem registro (fechar é
  decisão do dono).
- **Tempo e espaço**: 1 tick = 1 h; grade de células inteiras; deslocamento por rota com duração (`Mobility.TravelTicks`) e o
  cliente interpola por `routeArrivalTick`; interior por prédio, andar e célula local. Necessidades preguiçosas por tempo.
- **Interação de NPC**: comer, beber, comprar, trabalhar, conversar, dormir, buscar água, levar carga, recolher espólio,
  furtar, usar poder. **Agressão**: `Violence.Fight` + `Justice.OnCrime` (testemunha, boato, veredito, castigo), mágoa,
  vingança, psique; combate extraordinário.
- **Cliente**: HUD do jogador (ações, poderes, sair, pausa), `npc_actor.gd`, câmera ortográfica isométrica.

## Desenho (sobre o relógio do ADR-0050)
1. **Um relógio só.** Com jogador presente o mundo anda em Δ fino (ex.: 50 ms de mundo por tick a 1:1) para todos. Sem jogador,
   o ritmo do mundo é o configurado (proposta: rápido, Δ = 1 h). O jogador sozinho pode acelerar ocioso ou ao dormir (até um
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

## Critérios (a escrever ao ativar, `rules/eval-criteria.md`)
- Mesmo registro (entradas, Δ, acelerações) reaplicado do snapshot dá o mesmo hash; entrada contra parede não move.
- Velocidade cai com carga, ferimento e cansaço (par na mesma seed, direção em ≥18/20).
- Golpe do jogador e `Fight` do NPC com o mesmo par e seed: mesmo dano, mesmo caso, mesmo hash.
- Com dois jogadores conectados, todo comando de acelerar é recusado e o hash não muda.

## Decisões do dono (com recomendação)
1. Ordem do jogador no NPC tomado: movimento e golpe sempre obedecidos; recusa só em ação longa e no limite do corpo (recomendado).
2. Fechar `/visual/player/{id}/move` e trocar pelo comando `move` do canal (recomendado).
3. Tecla da 1ª pessoa e padrão 3ª pessoa (recomendado).
4. O que interrompe o sono acelerado: golpe, fogo, fome/sede crítica, alguém chamando (recomendado: os quatro).
