# Cidade viva: diagnóstico do "parados, não vão para casa" (#242)

Base: `4cf95790` (feat/gameplay-aaa). Período `cidade-media` (150 pessoas, mapa 20x20 de vila, 24 prédios), sementes 2 e 7,
3 a 10 dias de aquecimento e 5 dias medidos, amostra de hora em hora. Sonda: programa solto (fora do repo) que carrega
`DefaultPeriodSeeder.PayloadFor("cidade-media")` e lê o mundo; nada no motor mudou. Nenhum código foi alterado nesta entrega.

## 1. Motor: o que as 150 pessoas fazem (semente 2; a 7 dá o mesmo desenho)
Ação por hora (% das 150) e onde (casa / trabalho / outro prédio / rua / terreno aberto, %):

| Hora | Dorme | Trabalha | Viaja | À toa | Conversa | Come | Local c/t/o/r/ab |
|---|---|---|---|---|---|---|---|
| 03 | 87 | 4 | 5 | 3 | 0 | 0 | 97/1/1/0/1 |
| 08 | 5 | 58 | 3 | 23 | 10 | 0 | 42/57/1/0/1 |
| 14 | 12 | 11 | 45 | 22 | 9 | 1 | 39/24/2/0/35 |
| 16 | 12 | 5 | 17 | 63 | 2 | 1 | 41/33/2/2/22 |
| 19 | 11 | 4 | 15 | 20 | 47 | 3 | 57/22/2/1/18 |
| 21 | 12 | 5 | 35 | 19 | 1 | 29 | 68/16/2/0/14 |

- **Dormir fora de casa: 0%** em todas as horas. **Sem casa: 0%.** Casa e cama não são o problema.
- **Rua de verdade: 0 a 2%** das pessoas em qualquer hora; ninguém "mora" na rua, mas também quase ninguém a usa parado.
- **Adulto empregado às 15-17 h** (turno acaba às 14 h; 475 amostras por hora): 55% à toa ou conversando, dos quais 37%
  **dentro da planta do próprio posto**, 14% no terreno aberto em volta (lavoura) e só 3% em casa.
- **Às 18-20 h** (conversa): 28-34% em casa, 27-31% **no posto de trabalho**, 10-14% no terreno aberto.
- Corridas de 2 h ou mais parado fora de prédio, sem viajar (5 dias): 225 (sem 2) e 340 (sem 7), com 42 e 52 de 6 h.
  A maioria é "à toa em terreno aberto, de dia", sobretudo quem não tem emprego (450 horas) e o adulto empregado (196).
- Quem chega a casa só volta às 21-22 h para ceia e cama (viagem 35% às 21 h): o "voltar para casa" acontece, mas
  tarde. Exemplo (npc 122, posto a 8 células da casa): sai do turno às 14 h e fica à toa ou conversando **no posto até
  as 22 h**, come lá, e só então viaja para a cama.

### Causas no código
1. **Depois do turno não existe destino "ir para casa" nem "ir ao largo".** Só se volta se a casa está "fora de alcance"
   (`Carry.cs:145` `OffShiftFarFromPantry` -> `PersonalCarry.cs:49` `PantryOutOfReach`, "mais de uma hora de caminho"). Quem
   mora a menos de 1 h do posto fica onde o turno acabou até a hora de dormir (`Effects.cs:630`; destino da cama em
   `Destinations.cs:70`). A rotina (`ActionCatalog` / `RoutineBoundaries`) não tem faixa 15-17 h: cai em À toa.
2. **À toa e conversar andam 1 passo a cada 3 horas, ao acaso**: `StreetRules.Ad192.cs:79` `AmbientStepsPerHour = 1/3`,
   `Movement.cs:30` `AmbientSteps` e `MoveOneAmbientStep` (vizinho sorteado entre 8, sem destino, sem olhar prédio ou rua).
   Em 3 h reais o corpo muda uma célula; é "parado" para quem olha.
3. **Conversar não vai a lugar nenhum**: `Effects.cs:632` só viaja quem "não tem ninguém por perto" (`CompanyDestinationOf`); quem
   tem alguém ao lado conversa onde está (posto de trabalho, terreno). Não há largo, taberna ou praça como ponto de encontro.
4. **A viagem do NPC ainda custa a escala antiga (a maior causa, ver 2).** `Mobility.TravelMs` (`Mobility.cs:61`) usa
   `CostWeights.Base` = 0,05 h por célula; o ADR-0052 já aceitou metros reais, mas a parte AD-197o2 (viagem dos NPCs em
   metros) foi revertida (commit `b1896f67`, ainda por refazer) por 41 testes e limiares em horas.

## 2. Cliente: tempo real e andar
Medido com a API própria (porta 5422, mundo seed 2, jogador encarnado por `player:`), cruzando o motor:
- **Com jogador o mundo roda 1:1** (`SimulationHost.Plan`, `TimeScale=1`, tick de 50 ms): 5 s reais = 4 s de mundo.
- `Mobility.TravelMs` de casa ao posto de 12 empregados: **38 a 82 minutos do mundo para 6 a 16 m** (4 a 11 células), ou
  **0,0015 a 0,005 m/s**; o jogador anda a 1,4 m/s (~300 vezes mais). A mesma conta em hora cheia dá 2-3 ticks.
- Enquanto o jogador estava no mundo, **145 s reais seguidos sem uma única célula mudar para as 150 pessoas** (sonda a cada
  5 s no snapshot da cidade, `moved_since_last=0`; só as 35 do primeiro tick em hora cheia). Os 9 viajantes daquela hora
  andam a menos de 1 cm/s no desenho: a rota e o `routeArrivalTick` chegam (`LivingScopeProjector.cs:170`) e o cliente anda
  certo (`route_walk.gd`), mas o trajeto dura 40-80 min de relógio. **O mundo anda devagar; a projeção manda o trajeto.**
- Passeio e conversa: 1 passo em 20 min reais. Ninguém sai do lugar numa sessão de 5 a 10 min.
- Desenho: pessoa em prédio fica sob o telhado (3D oclui; `follow_director.gd:192`): à noite a cidade vista de cima está
  vazia (`fotos242/n3-out.png`, 03 h, 150 pessoas, 97% em casa) e de dia só aparecem os do terreno aberto e das lavouras (`h16-a.png`):
  quem se vê é justo quem está parado. No snapshot de interior da casa 9 às 3 h os 6 moradores vêm todos no andar 1, célula
  local (1,1) (empilhados; o cliente os espalha); minha captura do interior não mostrou corpos (câmera/ângulo, inconclusivo).
- Placa da cidade: "120 na rua · 0 no pool" conta quem está dentro dos limites da cidade, não quem está na rua
  (`city_renderer.gd:45`, `CityDensityVisual.Inside`): rótulo enganoso (às 16 h só 2% estavam em rua).

## 3. Proposta priorizada (nada implementado; custos em dias de agente)
| # | O que | Onde | Custo | Risco de desempenho |
|---|---|---|---|---|
| 1 | **Viagem dos NPCs em metros (AD-197o2)**: refazer `b1896f67` e rever os 41 testes e limiares em horas (alcance da despensa, espólio, obra de rua, "longe de casa") | motor | 2-4 | Neutro a melhor (viagens curtas, menos ticks em Travel); goldens e demografia mudam: medir fome e mortes 3x6 sementes |
| 2 | **Depois do turno vai para casa ou ao largo**: faixa de rotina 15-17 h = "voltar" (destino = cama/casa) e Conversar com destino = o largo/praça/taberna mais perto (reaproveita `CompanyDestinationOf`) | motor | 2-3 | Uma escolha de destino por início de ação, não por tick; custo por NPC-tick ~ igual |
| 3 | **Passeio por trechos curtos em metros** (destino 3-8 células, `TravelMs`) no lugar do passo sorteado a cada 3 h | motor | 1-2 | Só no início da ação; sem custo por tick. Depende do 1 |
| 4 | **Interior: corpos em células distintas e dorminhoco na cama** (hoje 6 na mesma célula) | motor + cliente | 1 | Só nas casas em foco |
| 5 | **Vida de espera no cliente**: olhar em volta, trocar o apoio, virar para quem conversa (só LOD perto) | cliente | 1 | Medir `stress:500`; só no raio da câmera |
| 6 | Rótulo "na rua" -> "na cidade" e acrescentar "em casa/no trabalho" (vem do motor) | cliente + API aditiva | 0,5 | Nenhum |

Ordem recomendada: 1 (destrava tudo; sem ele 2 e 3 não aparecem em tempo real) -> 2 -> 3 -> 4 -> 5 -> 6.
Não mexi em coleta/mata, ação em andamento, barra de progresso, câmera nem oferta de água.

Reprodução: sonda em `/private/tmp/claude-501/-Users-aoliveira-Desktop-Projects-LivingWorld/7392251a-1ec6-4bf2-8382-62ad34632fdc/scratchpad/probe242/Program.cs`
(`dotnet probe242.dll <seed> <dias> <aquecimento>`), capturas em `.../scratchpad/play/fotos242/`.
