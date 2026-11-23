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
`LW_AD194=off` desliga as duas regras (material em horas na abertura, sem evento de dia perdido), na vila de código
(`CityLayoutSwitch`) e nos mundos do menu (`MenuWorlds`).

## Fontes e confiança
Viagem de material a pé com padiola ou cesto (25 kg) e a madeira buscada na mata: mesma ordem de grandeza da AD-184 (Dyer, 2000),
precisa de conferência; a carga de 2 unidades por viagem é escolhida sem fonte.
