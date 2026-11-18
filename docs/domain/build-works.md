# Toda construção é obra (AD-184)

Nenhum prédio aparece pronto: a casa, a oficina, o prédio da fila da cidade e o trecho de teste de calçar são obra num lugar,
com material, horas e gente presente. O motor é a fonte da verdade. Ver também [site-work](site-work.md) (a turma e a agenda).

## Casa nova (`Cities/Works/BuildWorks`, diário)
- **Quem precisa**: a casa sem moradia (sem prédio, despejada), a que mora numa casa cheia além do que ela abriga, e o casal que
  foi morar de favor com a família de onde saiu (`NeolocalCouples`, o costume do noroeste europeu; `GroundLedger.Lodgers`); quem
  já morava no seu andar de uma casa de vários não conta. A família mais antiga da casa é a dona. Cada casa olha uma vez por
  semana (pelo id e pelo dia, sem sorteio). Só lote com porta para a rua (degraus de rua da AD-173): sem ele, segue apertada.
- **Onde mora enquanto isso** (`MarriageSystem`): a casa que um dos noivos deixou vazia fica com o casal; senão eles moram de favor
  na casa de um dos pais, a mais conveniente para os dois. Casa vazia na cidade (do falecido): a mais conveniente, sem obra.
- **Conveniência** (`HomePlaces`): o custo por ano de morar numa célula = as idas de cada dia pela rota — água (a fonte da
  AD-185, `WaterFetchFor`) e mercado de comida por morador (`WaterTripsPerMemberDay`, `MarketTripsPerMemberDay`), ida e volta ao
  trabalho de cada empregado — pelo valor da hora. O peso sai da casa: quantos são, quem trabalha e onde.
- **O lote**: a família compara lotes de rua (`LotChoices`, do largo para fora e perto do trabalho e da água de cada um) pela
  conveniência nos anos da casa mais o foro do quintal (`CroftFeeDaysPerCell` dias de jornal por célula, ao tesouro; o quintal que
  ela não paga fica de fora). Quem já mora (de favor, apertado) só muda se o lugar novo não custa mais que o de agora mais o que
  vale a casa própria (`OwnHomeDaysPerYear`, 30 dias de jornal por morador por ano); uma casa a horas do trabalho não compensa.
  O canteiro (prédio com `UnderWork`) ocupa o lote desde já; ninguém mora nem trabalha nele até a obra acabar.
- **Custo e prazo**: horas = `HouseHours` + quintal × `CroftHoursPerCell` + material. Material (`HouseMaterialResourceId`, madeira
  no `default.json`) comprado de quem vende na cidade se a casa pode pagar, e então só as viagens de carregar; senão
  `HouseGatherHours` de buscar na natureza (a vila de código não vende madeira).
- **Quem constrói**, de que depende: os adultos da casa nas horas livres de dia (fora das horas de trabalho da rotina, até
  `BuildDaylightEndHour`; não com fome ou sede abaixo de `HelperNeedFloor`; a mãe com filho de colo não vai), sem jornal; parentes (pais, filhos, irmãos) e vizinhos de afeto (≥ `MutiraoAffection`) em mutirão, se
  querem: vontade = laço × (meio + metade da generosidade) × a parte do dia que a ida deixa − metade da necessidade da própria
  casa (quem precisa de jornal vai à obra paga), até `HelpersMax`; e diaristas pagos se a casa pode: metade do que ela tem acima
  da reserva de cada morador paga a turma (sem isso, nenhum diarista). Pronta, a família muda para ela (evento com causa).

## Oficina de quem empreende (`VentureFounding`)
O mês da decisão paga o foro e o equipamento, assina o empréstimo e abre o canteiro; o resto do capital fica guardado na obra
(paga os diaristas; o insumo inicial e o giro ficam de reserva). O fundador segue no ofício de antes e ajuda nas horas livres com a
família. Pronta, a oficina abre com o dono, os sócios com cota, o insumo comprado então e o resto como caixa; o empréstimo passa a
ter a oficina como garantia. Atraso é custo real: as parcelas correm com a carência de sempre mesmo sem a oficina abrir.

## O jornal disputa a mão (`SiteWork.Evaluate`)
- **Oferta**: começa no jornal do dia (`PavingDecision.DayWage`); sem braço bastante, quem encomenda sobe `OfferRaisePm` por
  semana, até `OfferMaxPm` e até o que pode pagar pelas horas que faltam (tesouro, casas de frente, a família, o fundador).
- **Aceitar é decisão**: sem emprego, o maior entre a necessidade (moeda e despensa) e o atrativo do jornal, que cai com o que a
  casa tem guardado (`SiteComfortReserves`); com emprego, só se o jornal passa o salário pela margem — `QuitMargin` (1,25, a da
  troca de emprego), `QuitMarginSlack` (1,0) para o lavrador na entressafra, `QuitMarginPeak` (2,0) na semeadura e na ceifa —, mais
  forte com a necessidade. O dono da oficina e quem trocou de emprego há menos de 30 dias não largam. Tudo × a parte da jornada
  que a ida pela rota deixa. Primeiro quem mais quer (desempregado, jovem sem reserva, lavrador na entressafra, quem ganha pouco).
- Quem aceita com emprego **larga o ofício** (`Fired`, causa = a obra; 30 dias de carência de troca; `GroundLedger.OnLeave`):
  quando sai da turma (a obra acabou, foi desfeita), volta ao lugar de antes se ainda há vaga; antes disso a alocação de trabalho
  e a ceifa (`WholeVillageReaps`) podem tomá-lo, e ele sai da turma por "emprego". O ajudante leva água e lanche para as horas da
  obra depois do turno e, com a água da casa baixa (AD-185), vai primeiro à fonte.
- Painel do cérebro (`GET /npcs/{id}/brain`, `siteWork`): a obra dele (diarista, família, parente, mutirão) ou a melhor que
  contrata, com jornal, salário por dia, necessidade, parte do dia, valor, mínimo e o motivo de aceitar ou recusar.

## Obra sem gente
Sem nenhuma hora por `StallDays` (7): parada (`WorkStalled`); a turma voltando: retomada (`WorkResumed`); sem hora por
`SiteStaleMonths` (6): abandonada (`WorkAbandoned`), o canteiro sai e o lote volta a ser livre (o fundador recebe de volta o que
guardou). Consequência do atraso: o casal mora apertado mais tempo, a oficina abre mais tarde. A fila da cidade só abre obra num lote
a menos de meia jornada de alguma casa da cidade (lote que ninguém alcança não recebe turma) e abandona a que fica meses sem hora.

## Eventos com lugar (contrato por acréscimo)
`BuildWorkStarted`, `BuildWorkDone`, `WorkStalled`, `WorkResumed`, `WorkAbandoned`: `obraId|cidadeId|tipo|x|y|...` (tipo casa,
oficina, predio-N, calcamento); `StreetWorkStarted/Done` ganharam `x|y` no fim. O cliente recebe `involved.workId` e a posição
do evento; o prédio em obra traz `work` (id, tipo, progresso, parada, turma, ajudantes, oferta) e a obra encomendada vem como
processo `build-work` no lote.

## Chaves (todas ligadas; `LW_AD184=off` desliga todas e volta à AD-179)
`BuildNeedsCrew`, `HouseIsWork`, `VentureIsWork`, `HelpersBuild`, `WageBidding`, `PrototypeIsWork` (em `StreetRules`). Para
atribuir efeito nos relatórios: `LW_AD184=nohouse|nohelpers|nobid` desliga só a casa, só a ajuda ou só a troca de ofício.
Medir: `.medicao`-style pareado 40×15 (`demography-report`) e `MenuWorldsReport` (`predios`, `canteiros`, `semCasa`, `obras[...]`).

## Fontes e confiança
- Casa camponesa de madeira e taipa: algumas dezenas de dias-homem de carpinteiro e ajudantes, com material da terra e vizinhos
  (Dyer, *Everyday Life in Medieval England*, 2000; Hanawalt, *The Ties That Bound*, 1986) — `HouseHours` 480 e 160 h de
  material: ordem de grandeza, precisa de conferência. Casa neolocal no noroeste europeu (Hajnal, 1982) — média.
- Diarista no tempo morto da lavoura (Dyer, 1989) — média. Escolhidos sem fonte: margens 1,0/1,25/2,0, +10%/semana, teto 3×,
  foro de 2 dias por célula, 6 h por célula de quintal, 20 h de fim da luz, mutirão em afeto ≥ 50.
