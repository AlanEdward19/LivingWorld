# Refeição de rotina e a medida da aflição (AD-157, AD-157b)

Com a despensa em casa (`InventionRules.PantryNeedsPresence`, ligada no cenário de código desde o AD-157b), longe dela a pessoa
come do que leva, da refeição do empregador ou volta. Este documento diz como se mede se isso piora a vida e o que a pessoa faz
para não passar fome: comer em hora de refeição, não só na urgência. Despensa e compra: [household-provisioning](household-provisioning.md);
carga e recipiente: [invention](invention.md).

## A medida da aflição (`NeedDistressTally`)
`demography-report` imprime por seed (`aflicao seed=...`) as horas-pessoa por pessoa-ano com fome e com sede crítica (déficit
acima de 80), as de quem está longe de casa, as de criança (<12) e velho (65+) em casa, as horas de trabalho do adulto e as
mortes de fome e sede (longe inclusive). Compara-se a mesma seed em duas rodadas, nos primeiros 15 anos (40 seeds, ~6 min).
Ruído (AD-157b, cérebro ligado, desligar o sequestro): fome ±2,2 h (de 10,3), sede ±4,6 (de 512), dependentes ±7,7, trabalho
±0,05 h/dia. Por seed sai também a causa da fome crítica (o que a pessoa fazia: comendo, dormindo, trabalhando, viajando).

## A refeição de rotina (AD-157b)
Com a despensa em casa, comer só na urgência custava uma viagem com fome: 34 mil voltas para comer por seed (24,5 por pessoa-ano).
Medido: a fome crítica vinha de quem chegava em casa já crítico e passava a refeição de duas horas assim, de quem passeava à toa
para longe (o passo ao acaso; 85% das voltas saíam de célula sem local) e de quem ficava perto da lavoura depois do turno; com a
fome sozinha (a sede já servida), comer só vencia a conversa e o sono na crítica. Gente de verdade come em hora de refeição:
- **Desjejum** (`PersonalCarry.PackForTrip`): quem sai para além de uma hora de casa bebe e come antes só se chegaria à sede ou à
  fome crítica fora (`MealAheadDeficit` 80; antes disso a unidade inteira gasta antes da hora faltava no poço fraco); leva o resto.
- **Pausa do turno** (`WorkBreakMeal`): no próprio local de trabalho, com a fome ou a sede em `MealTimeDeficit` (65), come do que
  levou ou a refeição do empregador e bebe do poço da propriedade, sem largar o trabalho.
- **Hora da refeição em casa** (`MealTimeAtHome`): à toa ou conversando em casa com a fome em 65, come; deitado, a fome urgente
  levanta para comer. **Ceia** (`SupperBeforeSleep`): quem chegaria à fome urgente ou à sede crítica na noite come antes de deitar.
- Quem só foi beber em casa não come a ração com fome abaixo de `AppetiteDeficit` (40): a comida comida sem fome esvaziava a casa.
- **Fim do dia em casa** (`OffShiftFarFromPantry`): à toa longe da despensa, volta para casa; o passeio fica ao alcance dela.
- **Volta pelo que ficou** (08b item 77): a compra cortada pela carga marca a casa (`InventionLedger.LeftAtMarket`) e a ida seguinte
  sai sem esperar a despensa cair (antes, mediana de 120 h contra 79 h de uma ida normal). AD-182 (08c 147): a marca guarda quanto
  ficou de cada recurso (`LeftAtMarketQuantities`) e só antecipa a ida se o resto soma um dia de consumo da casa
  (`MarketTripRules.ReturnMinDays`); com menos, espera a compra de sempre. Na banca compra pela meta (o resto está nela).
- **O recipiente vai à compra** (AD-182, 08c 111): quem sai para comprar leva o recipiente da casa mesmo sem arrumar lanche
  (mercado perto) e o segura até a compra acabar; antes outro morador podia sair com ele e a carga caía para os braços.
  `MarketTripRules.HoldsCarrier`; medir sem as duas: `LW_MARKET_TRIP=off`.

| 40 × 15 (cérebro ligado) | fome crít. | sede crít. | longe | dep. em casa | trabalho h/dia | voltas p/ comer | mortes fome |
|---|---|---|---|---|---|---|---|
| desligada | 10,3 | 512 | 142 | 401 | 6,70 | 0 | 0,80 |
| ligada | 10,9 (+0,5 ± 2,1) | 177 (−335 ± 11) | — | 133 (−268 ± 13) | 6,79 (+0,09) | 3,1 por pa | 1,38 (± 0,40) |

O mundo muda para todos: a sede crítica cai 65% (dependentes em casa 67%), o adulto trabalha 1,4% mais e a fome crítica fica
igual. A despensa vazia em casa sobe de 20 para 31 h por pessoa-ano (come-se em hora certa, mais vezes). A carga que a pessoa leva
é estoque do mundo (`InventionLedger.CarriedTotal`, na conservação); a de quem morre longe de casa fica no chão
([leavings](leavings.md), AD-168); a de quem sai do mundo vai ao consumido.

## A refeição de quem come na oficina (AD-166, item aberto 67)
Quem vai ao próprio trabalho e lá ganha a refeição leva só o caminho mais a folga (`PackMarginHours`) e, na pausa, come
primeiro do que levou (`ConsumeFromPack`); a ração do empregador só vem com a carga vazia, e o que sobra volta à despensa.
40 × 15 (base): 8,8 refeições da pausa por pessoa-ano (`pausa_trab`) e 8,7 rações longe de casa (`racao_longe`, a mesma
ração vista pela compra: diferença de 1%), 12,4 refeições da carga, nenhuma hora de fome crítica comendo sem refeição
(`fc_comendo_sem_refeicao` 0); fome crítica no trabalho 1,5 h de 7,9 por pessoa-ano; 1,1 vez por pessoa-ano alguém fica
crítico longe sem nada. Cada ração sai uma vez do estoque e uma vez da caderneta: sem dupla contagem.

## De onde vêm os números da rotina (AD-166, item 112)
Fonte (popular, grau [C]): na Inglaterra medieval comiam-se **duas refeições** — o jantar no fim da manhã (10–11 h, no
campo para o lavrador) e a ceia ao pôr do sol —, e o desjejum era de quem trabalhava pesado ou viajava, e do ceifeiro
(tastesofhistory.co.uk; historyrevealed.com; medievalcookery.com; Boorde, 1542, é a fonte primária e é tardia). O desenho
confere: desjejum de quem sai longe, refeição na pausa do turno, ceia antes de deitar. Os limiares **não** têm fonte:
`MealTimeDeficit` 65, `MealAheadDeficit` 80, `AppetiteDeficit` 40, a folga de 12 h e o dependente (<12 ou 65+) são escolha
do modelo, confiança baixa. Escala: o consumo medido é 0,69 unidade por pessoa-dia (uma ração do dia), então a refeição
do modelo é parte da ração diária, não uma refeição de época. Nada contraria a fonte: nada foi ajustado.

## Ver também
- [behavior.md](behavior.md) — a decisão (urgência, rotina) em que a refeição de rotina entra
