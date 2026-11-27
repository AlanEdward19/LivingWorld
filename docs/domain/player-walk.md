# Andar do jogador (#248, M1 da fatia jogável #246)

O corpo conduzido anda solto, em frações de célula, à velocidade real do corpo (AD-197k). Desde o #248 quem anda é o
**motor**: o cliente prevê e corrige, nunca decide. Código: `Simulation/Players/PlayerWalk.cs`, `PlayerWalkSystem.cs`,
`PlayerGround.cs`; cliente: `player_hud.gd`, `player_body.gd`, `ground_grid.gd`.

## Motor: o rumo mantido anda a cada tick
- `walk {dx, dy, run, wseq}` só **muda o rumo** (milésimos) e numera o comando. Com tempo fino, o motor agenda UM evento
  `player-walk` por corpo (`FinePose.PumpMs`), que a cada tick anda o corpo pelo rumo e agenda o seguinte.
- Para sozinho (rumo zerado) se: o cliente fica `PlayerRules.WalkIdleStop` sem comando (1 s reais × escala), o corpo ocupa-se
  (ação longa), perde o dono, ou o tempo deixa de ser fino (salto de tempo e sono acelerado: não anda sozinho).
- Sem tempo fino (cenários de hora em hora) vale o de antes: anda o tempo decorrido desde o comando, no máximo `WalkHold`.
- Determinístico: tudo em milésimos, tempo de mundo, evento no agendador. Só corpo de jogador paga; custo por NPC-tick igual.
- O laço do relógio anda por prazo (`LoopPacer`): ciclo longo não atrasa o mundo contra o relógio real.

## Pose e reconciliação
- A pose devolve `fx/fy`, `speedMps`, `seq` (último `wseq` aplicado) e `atMs` (instante de mundo). Contrato só aditivo.
- O cliente guarda os últimos comandos (`seq`, rumo, instante). Ao chegar a pose do comando `seq`, ele a põe no instante
  de meia ida e volta atrás e **reaplica** os comandos que o motor ainda não viu, com a mesma regra de chão
  (`_engine_now`); o corpo converge aí aos poucos (`reconcile_to`); erro > 2 células salta (teleporte do motor).

## Colisão: uma regra só
- `GET /players/{id}/ground?r=N` (canal: `ground`) devolve a máscara de passos ao redor do corpo: por célula, um bit por
  vizinha (`(dy+1)*3+(dx+1)`). É `PlayerGround.TerrainRefusal` (a mesma do passo do motor): fora do mapa, parede, só entra e
  sai pela porta a partir da soleira, entre prédios nunca, andar de cima só dentro do prédio, canto de parede na diagonal.
- O cliente anda em trechos de 0,25 célula como o motor, desliza pelo eixo livre e consulta a máscara a cada troca de
  célula; sem a máscara (ainda não chegou) usa o controle antigo. Quando o #226 trocar parede-célula por aresta, só o
  motor muda: a máscara acompanha.
- Gente: sem desvio nem lentidão por geometria. Só a célula cheia (`crowdLimit`, `PlayerRules.CellCrowd`) barra, como no motor.
- Até o #226 o motor barra a célula inteira de parede: dentro de casa o corpo para uma célula antes do painel desenhado.
