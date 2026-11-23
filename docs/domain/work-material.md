# Material de verdade na obra e o dia perdido (AD-194)

Continuação de [build-works](build-works.md) (AD-184). O motor é a fonte da verdade; o cliente só desenha o que ele projeta.

## O material é estoque do canteiro (`Cities/Works/WorkMaterial`, `StreetRules.MaterialOnSite`)
- **O que a obra leva**: casa e oficina abrem com `HouseMaterialUnits` (20) de `HouseMaterialResourceId` (madeira no
  `default.json`) como plano (`BuildWork.MaterialNeeded`). As horas da parede não somam mais o material: horas = casa + quintal (a
  oficina, as do plano dela).
- **De onde vem cada carga**: quem vende na cidade (o mais barato com estoque, desempate por id) se quem paga pode pagar a carga
  agora — a casa (`PavingDecision.PayFrom`) ou o que o fundador guardou na obra (`Escrow`); senão buscada na natureza
  (`HouseGatherHours` por unidade, proporcional). `BuildUnitsPerLoad` (2) unidades por viagem, a ida carregada
  (`Mobility.LoadFactorOfKg` com `SiteLoadKg`) e a volta pela rota.
- **Quem carrega**: as horas de quem está na obra (diarista ou ajudante), contadas como sempre por `SiteWork.Clock`. Primeiro a parede
  usa o material que há no lote; o resto vai à viagem em curso (`HaulBanked`); cada viagem paga chega como carga
  (`WorkMaterialArrived`, com quem a trouxe e se veio da banca ou da natureza); as horas que sobram depois da carga voltam à parede.
- **A parede só sobe com material no lote**: o feito nunca passa de (posto + no lote) / total; o material passa do lote à parede na
  proporção das horas feitas. Pronta, todo o material está posto.
- **Travou por falta de material**: sem material no lote, sem quem venda (ou sem como pagar) e sem como buscar
  (`HouseGatherHours` = 0), a obra trava com `WorkMaterialShort` (uma vez por episódio) e retoma (`WorkResumed` com `material`) quando
  a carga volta a chegar. As horas sem carga não se acumulam além de uma jornada.
- **O que sobra**: obra pronta ou abandonada devolve o material do lote à despensa de quem encomendou (na oficina, a casa do
  fundador; sem casa, ao estoque da cidade). O comprado e já posto fica gasto.

## Quem carrega anda (AD-195, `Cities/Works/WorkHaul`, `StreetRules.HaulersWalk`)
- **A viagem** (`HaulTrip` na obra): enquanto o lote não tem material para uma jornada da turma inteira, alguém da obra (diarista ou
  ajudante, um por hora; com o lote vazio a turma toda) recebe uma viagem. O destino de trabalho dele (`SiteWork.DestinationOf`) passa
  a ser a fonte: a despensa da casa que encomenda se ela tem o material (`MaterialFromHome`), senão a banca mais barata com estoque que
  quem paga pode pagar (paga na hora da carga), senão a mata logo fora da cidade, alcançável a pé (as horas de cortar, trabalhando lá).
- **Na fonte** pega nas mãos (`Npc.PickUp`) o que aguenta: o teto de carga e o peso que ainda deixa andar (`CarryMaxKg` pelo peso da
  unidade); anda de volta pela rota com o passo mais lento da carga (`Mobility.LoadFactor`) e deposita no lote (`WorkMaterialArrived`
  com quem e de onde). As horas na fonte são jornada: o diarista recebe, o ajudante conta andamento; a parede só sobe no lote.
- **Interrupção**: fome, sede, noite e tempo ruim tiram a jornada como sempre; a carga fica nas mãos até ele voltar ao lote. Quem sai da
  obra com a carga a leva para casa; a viagem que nunca chega à fonte em três dias passa a outro.
- **A fila da cidade** (`QueueHaul`): a turma do prédio-N busca no largo (estoque da cidade) o insumo da receita, carga por carga, e o
  passo da receita só consome o que está no lote (`ConstructionProject.MaterialOnSite`); sem insumo no estoque nem no lote, trava
  (`WorkMaterialShort`), e o dia perdido pelo tempo e a retomada saem com lugar como na casa. Pronta ou desfeita, o que sobrou volta ao
  largo. Projeção `materials[].onSite` também na fila.

## O dia perdido pelo tempo (`StreetRules.WorkDayEvents`)
Ao amanhecer, com gente na turma, se o lote não deixa trabalhar (chuva forte, neve, chão congelado: `WeatherNow.OutdoorWorkable`,
a mesma regra que tira a jornada), a obra registra `WorkWeatherLost` com o motivo (`chuva`, `neve`, `gelo`) uma vez por episódio, e
`WorkResumed` com `tempo` quando o tempo abre.

## Eventos (contrato por acréscimo)
`WorkMaterialArrived`: `obraId|cidadeId|tipo|x|y|recurso|unidades|quem|origem`; `WorkWeatherLost`: `obraId|cidadeId|tipo|x|y|motivo`;
`WorkMaterialShort`: `obraId|cidadeId|tipo|x|y|recurso|falta`; `WorkResumed` ganhou o motivo no fim (`|tempo`, `|material`).
O cliente recebe `involved.workId`, `involved.motive` e o lugar; a projeção dá `works[].materials` também para casa e oficina, com
`onSite` (no lote), `placed` e `needed`, `haulHoursLeft` e `halt = material`. O Godot desenha a pilha pelo `onSite` e lê os eventos
do log (antes inferia a parada do estado).

## A cidade média sem obra (diagnóstico)
`MenuWorksProbe` (`LW_WORKS_PROBE_OUT`): em d8609a05 os casais de favor da cidade média nunca abriam casa porque
`CityOccupancy.CandidateLots` não achava lote de rua dentro dos limites da cidade e da borda que ela absorve (seed 2: 0 lotes; sem o
limite, 7; limites de 19×16 a 22×16 com 63 prédios). Com a AD-192 (bairro denso, 2e0852ed) a seed 2 abre 2 casas e 2 oficinas em 8
anos e a seed 1, 4 casas; a seed 7 segue sem lote (119 de 120 consultas dos casais de favor: `sem-lote`). O teste
`MenuWorkOpensTests` cria o mundo pelo caminho do menu (seed 1) e vê a obra da casa abrir e receber carga.

## Chaves
`LW_AD195=off|nowalk|nohome` (carregador que anda; despensa primeiro). `LW_AD194=off` desliga as regras da AD-194 (material em horas na abertura, sem evento de dia perdido), na vila de código
(`CityLayoutSwitch`) e nos mundos do menu (`MenuWorlds`).

## Fontes e confiança
Viagem de material a pé com padiola ou cesto (25 kg) e a madeira buscada na mata: mesma ordem de grandeza da AD-184 (Dyer, 2000),
precisa de conferência; a carga de 2 unidades por viagem é escolhida sem fonte.
