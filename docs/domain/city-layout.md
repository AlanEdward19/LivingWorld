# Traçado da cidade e rua de verdade (AD-173, AD-176)

A vila cresce ao longo de ruas que saem do ponto de fundação, cada prédio nasce num lote escolhido por quem constrói, e a rua
que existe é a que o uso fez (AD-176). O motor é a fonte da verdade; o cliente desenha só o que ele expõe.

## Traçado reservado (`CityStreets`, `Layout/StreetNetwork`)
Função pura do mapa, do ponto de fundação e de `CityLayoutRules`: **largo**, **rua principal** no eixo mais seco, **travessas** e
**vielas**. Desde a AD-176 é só **reserva de espaço**: ali não se constrói, e a porta de cada lote dá para ela. Nada anda "pelo
traçado": a rua efetiva é o chão pisado. O traçado vira rua quando as pessoas passam por ele (as portas dão para lá).

## Chão pisado (`Streets/GroundLedger`, `StreetGroundSystem`, mensal)
- Toda viagem que chega soma uma passagem em cada célula da rota que fez (no fim do mês), fora prédio e água. O pisoteio
  acumulado perde `WearRetainPm` por mês (o mato volta). Snapshot e hash: linhas esparsas por célula, viagens do mês, obras.
- A cada `ReviewMonths` a superfície muda: **trilha** a partir de `TrailWear`, **terra batida** a partir de `DirtWear` (some abaixo
  da metade do limiar: folga). Cascalho e pedra só por obra. Prédio novo vira obstáculo na virada do mês e apaga o chão debaixo.
- Lama na chuva: **não entregue**, o motor não tem chuva nem umidade (o fator de clima é de safra). Dívida no 08d.
- **Chão da história** (AD-179, `StreetGroundSystem.SeedHistory`): o mundo começa no meio da história. Na criação, o mês típico
  (cada adulto vai e volta do trabalho do ofício dele, o mais perto, todo dia; cada casa vai `HistoryMarketTripsPerMonth` vezes
  ao mercado mais perto) deposita passagens pelas rotas, e o pisoteio vai ao valor estacionário do processo mensal (passagens ÷
  (1 − `WearRetainPm`)); a revisão faz trilha e terra batida. `HistoryRounds` = 2 (a 2ª rodada segue a rede da 1ª); 0 = nasce sem
  caminho. Fecha o 08d item 180.

## Rota (`Streets/StreetRoutes`)
- Pontas na **soleira** do prédio onde a pessoa está (por ela se entra e sai). Duas opções, fica a mais rápida: a **reta** pelo
  terreno (custo do mapa × passo médio das células, mais, por trecho de prédio atravessado, a volta em torno do solto
  `DetourPm` = 2√2−2 célula ou a subida por cima do colado `OverPm`), ou **ir ao caminho mais perto**, seguir a **rede**
  (trilha+, A* de 8 vizinhos) e sair perto do destino. Passo: sem caminho 1,00; trilha 0,85; terra 0,80; cascalho 0,75; pedra 0,70.
- O tempo de toda viagem (trabalho, cama, mercado, água, ajuda mútua, pressão de tecnologia) sai da rota; o vau da invenção soma
  a água da rota. Cache por par origem–destino, refeito quando a rede (revisão) ou os obstáculos (mês) mudam.
- **Veículo de terra** (AD-153) só leva carga se a rota tem até `HaulerOffRoadCells` células sem caminho; senão, braços.
- O NPC escolhe a rota mais rápida: isso concentra a passagem e decide quais trechos crescem (o laço).

## Calçar é obra (`PavingTechnique`, `PavingDecision`, `PavingWorksSystem`)
AD-179: a obra só anda pelas horas de diaristas presentes no trecho, que aceitam pela utilidade ([site-work](site-work.md)).
- **Técnica**: `TechDiscoverySystem` (anual) abre um trecho de teste numa cidade com `PavingDirtCells` de terra batida e alguém
  hábil; nasce a tecnologia `paving` (espalha por vínculo, como a de mobilidade). Eixo de habilidade ≥ `StoneSkillAxis` → pedra.
- **Decisão** (mensal): o trecho de terra mais passado vira obra se o tempo poupado em `PaybackYears` paga a mão de obra.
  Pagam as **casas de frente** (soleira no trecho) se todas podem; senão o **tesouro** (pavage).
- **Material e mão de obra**: horas de assentar + buscar o material pela rota (`LoadsPerCell` idas e voltas): pedra comprada no
  mercado que vende o recurso `StoneResourceId` (pedreira; estoque consumido, dinheiro ao caixa dela), ou, se o mundo não tem esse
  recurso, do terreno `StoneTerrainId`; cascalho do leito (água). Diaristas: adultos sem emprego que aceitam (AD-179), jornal por hora no trecho.
- **Manutenção**: a condição cai com o tempo e as passagens; abaixo de `RepairBelowPm` pede conserto; a zero, volta a terra.

## Acesso a toda casa
`AccessOf`: `street` (soleira em caminho ou no traçado), `path`, `alley` (viela: os dois lados da soleira são prédio), `stair`
(soleira tapada por sobrado vizinho: escada), `roof` (laje/telhado do vizinho), `none` (porta para fora do mapa: não deve
existir). A passagem por cima tem custo próprio na rota (`OverPm`).

## Lote (`Layout/LotPlanner`, `LotProfiles`) e degraus (`PlacementPolicy`)
Como na AD-173: oficina/mercado na principal; pobre casebre parede-meia; artesão; lavrador com quintal; centro sem quintal salvo
remediado; degraus 0–5 quando falta terra. Quintal: `Building.CroftCells`.

## Cidade-filha pela causa real
O aglomerado que nasceu longe por falta de lote perto só funda cidade se, pela rota, quem mora nele gasta em média
`FoundingCommuteHours` ou mais para chegar ao que a mãe dá (largo ou mercado, o mais perto) + o trabalho fora do aglomerado.
10 anos, `default.json` seeds 1/2/3/7: filhas/fusões 20/12 (rua desligada 19/11; antes do traçado 26/19).

## Contrato (só acréscimo)
- `BuildingVisual.Lot.access`: `street|path|alley|stair|roof|none` (ausente sem a rua de verdade).
- Camada `Roads` e `LivingScopeState.roads` (escopos Cidade e Mundo), delta `roadUpserts`/`roadRemoved` (chave = `cell`):
  `StreetCellView(city, cell, surface, users, main, square)` + `wear`, `useBand` (0–4: <10, <30, <100, <300, 300+
  passagens/mês), `planned`, `condition?` (0–1000, calçado), `work?` = `{surface, progressPct, repair, payer: treasury|
  frontage}`. `surface` vai como número: 0 trilha, 1 terra batida, 2 cascalho, 3 pedra. `users` = passagens do último mês.
- `NpcVisual.route?`: células da rota (soleira a soleira, pontas dentro do prédio) de quem viaja, e `routeArrivalTick?`.
  O cliente anda o boneco por elas entre o início da ação e a chegada; ausente = não viaja.

## Fontes e confiança
- Trilha que nasce do uso e some sem ele: Helbing, Keltsch e Molnár, *Nature* 388 (1997) — alta no mecanismo. Pisoteio:
  Cole, *J. Appl. Ecol.* 32 (1995), centenas de passagens tiram a cobertura — média; limiares 150/1500 e 15%/mês escolhidos.
- Passo por superfície: coeficientes de terreno de Soule e Goldman (1972: asfalto 1,0, estrada de terra 1,1, mato 1,2) e o
  fator 0,6 fora da trilha de Tobler (1993); 0,85–0,70 ficam entre as duas — média.
- Calçamento pago por quem mora de frente e concessão de *pavage* nas cidades inglesas do séc. XIII em diante — média.
- Escolhidos (sem fonte): horas por célula (40/120), viagens de material (4), equipe (6×8 h), retorno (10 anos), desgaste.
- Fundação: 2 h de caminho, na ordem do orçamento diário de deslocamento (Marchetti, 1994, ~1 h/dia) — baixa.

## Medido (AD-176)
Vila de código, seeds 1/2/7, anos 1/10/20: chão trilha/terra 190/47 → 30/116 (seed 1; a rede se concentra), traçado usado 9–20%,
trilhas fora do traçado 113–182, h casa→trabalho 1,46 → 1,16, casas sem rota 0. Com a tecnologia ligada, 1–3 obras de pedra em
20 anos (8–23 células) e casa→trabalho 1,25 → 0,84 h. `cidade-media` (antes da AD-179): ~1 viagem/pessoa-mês, nenhuma trilha.

## Mundos do menu (AD-179)
Os três períodos do menu eram fixture (economia desligada, sem oficina, mercado nem água da natureza, célula de 1 h, mapa 10/20/32
para 40/150/400 pessoas): ~1 viagem por pessoa-mês, chão nenhum, e a metrópole morria de sede em 2 meses nas seeds 1, 2 e 7.
Agora são a vila de código em três tamanhos (`DefaultPeriodSeeder`: economia, fazenda com poço e forja por centena, presença,
empreendimento, crédito, tecnologia ligada, célula de 30 min, lado pela regra da vila). Seeds 1/2/7, mês 12: 49–75 viagens por
pessoa-mês, casa→trabalho 0,7–2,3 h, 60–1.400 células de trilha e terra, zero morte de fome ou sede, zero casa sem rota;
`cidade-media` seed 2 em 20 anos: 17 obras de calçamento, 56 células de pedra, 65 entradas de diarista. Aceite: `MenuWorldsLiveTests`.
`LW_MENU_WORLDS_OUT=arq ... --filter MenuWorldsReport` (diagnóstico) e `MenuWorldsCostReport` (custo).
## Buscar água, emprego perto e dados de rota (AD-185)
`WaterFetch`: com a água da casa abaixo de 4 dias por morador, quem está em casa ou perto com tempo livre (8+ anos, fora da cama; quem
tem emprego só abaixo de 1 dia ou sem outro livre perto) vai à água mais perto pela rota (até 3 h, raio 6), bebe, enche o cântaro (20
L, recipiente ou veículo da casa) e volta carregado; a despensa só recebe o que chegou; a ida segue até o fim (a escolha da hora não a
recomeça) e vem antes até da mudança de cidade. Sede urgente (qualquer idade que ande) sem água vai à fonte mais perto de onde está (até
6 h) e bebe lá, ou volta à despensa se ela é mais perto; casa sem ninguém apto recebe água dos seus, senão do vizinho mais perto.
Rede: A* em lista de vizinhos com heap em vetor e heurística da menor taxa da rede; o trecho nem é buscado quando o limite inferior
não vence a reta. Emprego: a oficina do ofício com vaga mais perto de casa (`StreetRules.NearestJob`). Contrato: `routeDepartedTick`,
`routeOver`, `lot.accessCells`, `accessViaBuildingId`, `accessState`. `LW_AD185=off` desliga.
## Medir
`LW_CITY_LAYOUT_OUT=arq bash scripts/test.sh --project integration --filter CityLayoutReport` (diagnóstico, com a linha da
rua); `LW_STREETS=off` desliga a rua de verdade (relatórios, Workers, fixture de escala); `LW_CITY_LAYOUT=off`, o traçado.
