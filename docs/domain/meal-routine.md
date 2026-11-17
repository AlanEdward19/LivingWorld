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
- **Desjejum** (`PersonalCarry.PackForTrip`): quem sai para além de uma hora de casa e ficaria fora mais do que a sede espera bebe
  antes; a comida, só se chegaria à fome crítica fora (`MealAheadDeficit` 80); leva o resto que couber.
- **Pausa do turno** (`WorkBreakMeal`): no próprio local de trabalho, com a fome ou a sede em `MealTimeDeficit` (65), come do que
  levou ou a refeição do empregador e bebe do poço da propriedade, sem largar o trabalho.
- **Hora da refeição em casa** (`MealTimeAtHome`): à toa ou conversando em casa com a fome em 65, come; deitado, a fome urgente
  levanta para comer. **Ceia** (`SupperBeforeSleep`): quem passaria a noite até a fome ou a sede urgente come antes de deitar.
- Quem só foi beber em casa não come a ração com fome abaixo de `AppetiteDeficit` (40): a comida comida sem fome esvaziava a casa.
- **Fim do dia em casa** (`OffShiftFarFromPantry`): à toa longe da despensa, volta para casa; o passeio fica ao alcance dela.
- **Volta pelo que ficou** (08b item 77): a compra cortada pela carga marca a casa (`InventionLedger.LeftAtMarket`) e a ida seguinte
  sai sem esperar a despensa cair (antes, mediana de 120 h contra 79 h de uma ida normal).

| 40 × 15 (cérebro ligado) | fome crít. | sede crít. | longe | dep. em casa | trabalho h/dia | voltas p/ comer | mortes fome |
|---|---|---|---|---|---|---|---|
| desligada | 10,3 | 512 | 142 | 401 | 6,70 | 0 | 0,80 |
| ligada | 9,3 (−1,0 ± 1,7) | 156 (−356 ± 12) | 5 | 127 (−274 ± 12) | 6,87 (+0,17) | 2,3 por pa | 1,18 (± 0,42) |

O mundo muda para todos: a sede crítica cai 70% (dependentes em casa 68%), o adulto trabalha 2,5% mais e a fome crítica fica
igual. A despensa vazia em casa sobe de 20 para 29 h por pessoa-ano (come-se em hora certa, mais vezes).

## Ver também
- [behavior.md](behavior.md) — a decisão (urgência, rotina) em que a refeição de rotina entra
