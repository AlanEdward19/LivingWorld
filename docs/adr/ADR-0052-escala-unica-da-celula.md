# ADR-0052: escala única de 1,5 m por célula (tudo em metros reais)

- **Status**: aceito pelo dono em 2026-11-24 ("1,5 m por célula"; "precisamos rever a escala (realista)")
- **Data**: 2026-11-24
- **Decisores**: dono do projeto; rascunho por agente (AD-197o)

## Contexto
A grade tinha duas escalas. A viagem dos NPCs (`Mobility`, `StreetRoutes`) custava `CostWeights.Base` = 0,05 h por célula de terreno
leve (3 min a pé, ~250 m), enquanto a planta dos prédios, o desenho do cliente e o passo solto do jogador (AD-197k) contavam metros de
gente. Um NPC e o jogador com o mesmo corpo atravessavam a mesma distância em tempos ~170 vezes diferentes. Nas capturas em 1ª e 3ª
pessoa o dono viu casas quase da altura das pessoas e tudo amontoado.

## Decisão
1. **Uma célula = 1,5 m para tudo**, no motor: viagem dos NPCs, passo do jogador, planta, ruas, lotes, objetos e o desenho (o cliente
   só desenha o que o motor diz). A constante mora em `WorldScale` (Domain).
2. **Andar custa metros**: `Base` = 1,5 m ÷ 1,4 m/s em horas (≈ 1,07 s por célula de terreno leve); a subida
   escala junto. Terreno, rua, corpo, carga, chão do dia e cansaço seguem multiplicando como antes.
3. **Jogador e NPC pela mesma conta**: a velocidade do passo solto é a distância de um trecho à frente no tempo que a viagem de qualquer
   NPC leva nele (`PlayerWalk.SpeedMps` sobre `Mobility.TravelMs`).
4. **Medidas-alvo** (o gerador semeado as produz; nada fixo por mundo):
   - pessoa adulta: 1,50 a 1,90 m pelo corpo do motor (`Npc.Height`), criança pela idade;
   - andar de casa: 2,8 a 3,0 m de pé-direito, mais o telhado; porta ~2,0 m de altura e ~0,9 m de largura;
   - casa comum: 6 × 8 m a 8 × 12 m (4 × 5 a 5 × 8 células); oficina, celeiro e templo maiores, pelo tipo;
   - rua principal: 4 a 6 m (3 a 4 células); viela: ~2 m (1 a 2 células); recuo e quintal pelo lote, sem casas coladas sem razão;
   - objetos, móveis, árvores e lavoura na mesma escala (cama ~2 × 1 m; árvore 4 a 15 m; canteiro em metros).
   Referências de ordem de grandeza (sem texto copiado): plantas de casas camponesas e burguesas da Europa medieval e de vilas
   escavadas (casas de 5 a 12 m de frente, um ou dois andares), larguras de ruas medievais (vielas de 1,5 a 3 m, ruas de 4 a 8 m) e
   o passo humano de 1,3 a 1,5 m/s em chão firme.
5. **O mapa cresce pelo gerador semeado** quando a vila precisar de mais chão (ruas largas, lotes maiores), nunca por número fixo.

## Consequências
- **Distâncias e horários**: a vila de 100 pessoas tem ~55 células de lado (~82 m); atravessá-la leva cerca de um minuto a pé (antes,
  ~2,7 h). Sem jogador (hora cheia) toda viagem entre lugares distintos ainda custa ao menos um tick; com jogador (passo fino), segundos.
  O tempo que os NPCs gastavam andando volta para o trabalho, o descanso e a convivência.
- **Mapa**: as outras vilas do mesmo mapa ficam a dezenas de metros; distâncias de vila a vila de 1 a 5 km pedem mapas de 700 a 3.300
  células de lado (memória e rota): fica para uma parte própria, com região além da vila e LOD.
- **Goldens e baselines** mudam: regravados com a explicação na linha da AD-197o.
- **Entrega em partes**:
  - AD-197o (feito): este ADR, `WorldScale` no motor e `scale` em `GET /world/info`; o cliente desenha andar (3 m), porta (2 m) e
    janela nas medidas do motor; teste de pessoa × andar × porta e medida da largura das ruas.
  - AD-197o2: viagem dos NPCs em metros (`Base` pela escala) e o passo do jogador pela mesma conta. Provado à parte (commit
    `b1896f67`, revertido): mundos do menu 3 × 6 sementes por 10 anos sem fome nem colapso, mas 41 testes de Unit e os limiares em
    horas (alcance da despensa, ida ao espólio, obra de rua, "longe de casa") precisam ser revistos em metros antes de entrar.
  - AD-197o3: plantas, ruas (4 a 6 m), vielas, lotes e quintais em metros; o mapa cresce pelo gerador.
  - AD-197o4: objetos, móveis, árvores e lavoura na mesma escala.
