# Traçado da cidade: ruas, lotes, quintal e caminho (AD-173)

A vila não nasce em grade nem em anel: cresce ao longo de ruas que saem do ponto de fundação, e cada prédio nasce num
lote escolhido por quem constrói. O motor é a fonte da verdade; o cliente desenha só o que ele expõe.

## Ruas (`CityStreets`, `Layout/StreetNetwork`)
- Função pura do mapa, do ponto de fundação e de `CityLayoutRules` (nada no snapshot): um **largo** em volta do ponto
  de fundação, uma **rua principal** no eixo com mais terra seca, **travessas** a cada `LaneSpacingCells` (±1 por hash) e
  **vielas** de segunda ordem nas travessas. Cada rua atravessa uma célula de água só (vau) ou contorna com um passo de lado, e para no fim do mapa, na água
  sem contorno, ao encostar em outra rua ou em `MaxStreetCells`.
- Rua não recebe prédio, salvo no último degrau (escassez extrema). A passagem da porta até a rua também não.
- **Rua existe pelo uso**: só aparece (camada `Roads`) a célula que leva algum prédio ao largo. A superfície vem de
  quantas pessoas (moradores e quem trabalha no prédio) passam por ela: trilha abaixo de `TrodResidents`, terra batida
  daí para cima. Cascalho e pedra existem no tipo, mas nenhuma técnica do motor calça rua ainda (dívida, 08d).

## Lote (`Layout/LotPlanner`, `LotTerms`, `LotProfiles`)
Quem constrói escolhe o lote pelo que é e pelo que pode:
| Quem | Lote |
|---|---|
| Oficina, mercado, celeiro | Porta na rua principal (procura ali primeiro), recuo, sem quintal |
| Pobre (dinheiro/pessoa < `PoorMeansFraction` × mediana da cidade) | Casebre: porta na rua, parede-meia, sem quintal |
| Artesão | Porta na rua; no centro parede-meia e sem quintal; fora, quintal de `ArtisanCroftCells` |
| Lavrador | Quintal da frente (`FrontYardCells`) e croft de `FarmerCroftCells` (+1 se a casa tem `LargeFamilyMembers`) |
| No centro (rua a até `CentralDepthCells` do largo) | Sem quintal, salvo remediado (> `RichMeansFactor` × mediana) |
- A casa fica o mais perto do largo possível: de frente para a rua ou, se for mais perto, com um **caminho a pé** de até
  `PathCells` células da soleira até a rua. O quintal é escolha: a família só se afasta `CroftDetourCells` a mais por ele.
- Quem grava a posição (semeadura, obra, oficina nova) vira a porta para a rua; prédio derivado mantém a orientação da
  identidade. O quintal escolhido fica em `Building.CroftCells` (snapshot e hash).
- Na fundação todos têm o mesmo pecúlio: a faixa de meios só separa casas construídas depois.

## Quando falta terra (`PlacementPolicy`)
Degraus, cada um só depois que o anterior não achou lugar em todo o alcance: 0 o lote escolhido; 1 sem quintal; 2
parede-meia; (a semeadura usa um andar livre de casa existente aqui); 3 folga de uma célula sem rua; 4 encostando; (a
semeadura divide a casa menos cheia: aperto); 5 só sem sobreposição. Nos degraus 3–4 a soleira fica livre. Traçado
desligado: a ordem de antes (todos os degraus, depois o andar livre). A cidade crescida absorve o prédio a até `AbsorptionRingCells` do
limite **já crescido** (encadeado), mas só até `ChainedBoundsReachCells` (6) da caixa por população: a casa que nasce mais
longe por falta de lote perto forma aglomerado que pode fundar cidade-filha e depois se fundir à mãe (filhas em 10 anos, seeds
1/2/3/7 do `default.json`: antes 1/8/9/8, agora 1/4/6/8; fusões 1/5/6/7 → 1/2/3/5; `CityFoundingReport`).

## Consumidores
- **Valor do imóvel** (aluguel e diária, `BuildingValuation`): cada célula de quintal soma `CroftValuePerCell` ao fator
  de local.
- **Distância**: lote, caminho e rua decidem onde a casa fica, e a viagem (casa → trabalho, mercado, água) custa por
  célula. Horta, bichos e poço no quintal ainda não existem no motor (dívida, 08d).

## Contrato (só acréscimo)
- `BuildingVisual.Lot`: `Croft` (células do quintal, cercado), `FrontPath` (passagem/caminho da porta à rua), `Street`.
- Camada `Roads` modelada: `StreetCellView(City, Cell, Surface, Users, Main, Square)`.

## Fontes e confiança
- Toft (casa e terreiro à frente, na rua) e croft (faixa de cultivo atrás, até uma rua de trás) na aldeia inglesa medieval:
  Beresford e Hurst (orgs.), *Deserted Medieval Villages* (1971); escavações de Wharram Percy; C. Taylor, *Village and
  Farmstead* (1983). Confiança **alta** no padrão, **sem número usado**: a célula do modelo não tem metragem.
- Ruas de aldeia de terra, lama na chuva; calçamento só em cidade, com concessão de *pavage* (Inglaterra, séc. XIII em
  diante). Confiança **média**, precisa de conferência.
- Escolhidos (sem fonte): todos os números de `CityLayoutRules` (largo 1, travessa 13, centro 5, quintal 2/1, família 5,
  pobre 0,5 e remediado 2× a mediana, desvio 4, caminho 8, uso 12, valor 0,02 por célula).

## Medir
`LW_CITY_LAYOUT_OUT=arq bash scripts/test.sh --project integration --filter CityLayoutReport` (diagnóstico);
`LW_CITY_LAYOUT=off` desliga a regra (relatórios e diagnóstico) para medir pareado.
