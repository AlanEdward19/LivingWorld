# O espólio de quem morre longe de casa (AD-168)

Pedido do dono (2026-11-17), sobre a carga que voltava à casa em toda morte: "depende de onde morreu, se morreu longe da casa,
fica lá, para alguém buscar ou ser roubado". Código: `Simulation/Invention/Leavings.cs`; regra `InventionRules.Leavings`
(`LeavingsRules`, ligada por padrão; nula no snapshot quando é a padrão). Carga e recipiente: [invention](invention.md);
a refeição longe de casa: [meal-routine](meal-routine.md).

## O que acontece na morte
- **Em casa** (na célula da casa): a carga vai à despensa e o recipiente fica com a casa, como antes (`LeavingsAtHome`).
- **Longe**: a carga (`InventionLedger.LeftLoads`) e o recipiente (`Leaving.ItemId`) ficam no chão onde a pessoa caiu
  (`InventionLedger.Leavings`), com a casa a que pertenciam e, se o morto era o último dela, a casa do primeiro herdeiro
  (`EstateSystem.HeirsOf`). Nada se teletransporta. Evento `LeavingsLeft`, com a morte como causa.
- **Quem estava junto** e é da casa (acordado, a partir de `MinFetcherAgeYears`) recolhe na hora o que couber.
- Sem casa nem herdeiro vivo, o espólio é **achado**: quem passa leva sem furto. Morto sem chão de casa (sem-teto), como antes.

## Quem busca
A casa sabe da morte e do lugar na hora: é o mesmo instante em que o luto chega (`LifeEventIntake.Mourn`). **Dívida de
realismo** (08c): a notícia devia chegar por quem estava junto, por quem passa ou pela vila, não por onisciência.
- Fora do turno (`NonUrgentCandidate`, como a compra), um adulto da casa que pode buscar (a do morto enquanto vive alguém
  nela, senão a do herdeiro) sai para o espólio mais antigo ao alcance de `MaxFetchHours` (24 h de caminho), sem ninguém
  já a caminho e só se consegue levar algo (líquido pede parede estanque). A ida é uma viagem comum (`TravelDestinationOf`):
  arruma o lanche, custa o caminho e chega quando chegaria.
- Na chegada (`OnArrived`), leva o recipiente (se não leva outro, passa a usá-lo; se já leva, carrega-o para casa sem usar)
  e o que couber no peso e volume que sobram (`FootCarryKg`, mãos, recipiente), a água primeiro. O resto fica para outra
  ida. Volta para casa pela rotina; em casa, o que trouxe vai à despensa (`Unpack`). Evento `LeavingsCollected` (casa,
  herdeiro, achado). Gente da casa que chega ao lugar por outro motivo (trabalho, compra) também recolhe.

## Quem furta
Quem chega ao lugar, é de outra casa e tem idade de furtar pensa em levar pelo furto de sempre (AD-150, `Theft.Commit`,
alvo `espolio`): ninguém da casa ali de guarda; o temperamento (peso de Furtar) contra a barreira — a fome a rebaixa
(`NeedBarrier`), sem fome é a do furto planejado (`PlannedBarrier`), a dissuasão sobe as duas e cada pessoa acordada no lugar
(até 3) soma `SeenBarrierPerWitness` (0,15) e, AD-182, o laço de quem passa com o morto soma `KnownDeadBarrier` (0,5) vezes o
maior entre confiança e afeto (0–1): o que era de um conhecido não se leva —; a chance é `TheftChancePm` (250‰) vezes a
inclinação, no sorteio semeado.
O ladrão leva o que couber na carga (vai para casa com ele); a vítima é o chefe da casa dona; justiça, testemunhas e luta no
flagrante como em todo furto.

## Estrago e conservação
AD-182 (08c 156): uma vez por mês, cobrando só os dias que o espólio passou no chão (`CountDaysOnGround`), o perecível estraga
na taxa da despensa vezes `GroundSpoilagePm` (3000‰: ao relento pega chuva e orvalho e fica ao alcance de rato, cão e ave) e a
água se perde a `GroundWaterLossPerDayPm` (20‰/dia: evapora e vaza do recipiente largado; antes nunca se perdia); as duas
crescem com o calor do lugar (`GroundQ10` 2 a cada 10 °C acima de 15 °C; o frio conserva). Pelo mesmo `ResourceLost`
(payload `espolio-<id>|recurso|unidades`). A peça que vence no chão some com o item. Os testes de conservação somam despensas +
`CarriedTotal` + `GroundTotal`. Medir o de antes: `LW_GROUND=old`.

## Medida (40 seeds × 15 anos, pareado com `LW_LEAVINGS=off`)
28 espólios em 600 seed-anos (mais 11 mortes em casa com carga): 7 idas de busca, 19 recolhidos pela casa, 4 achados sem
dono, 5 furtados, nada estragado. Natalidade 1,005× (IC90 1,001–1,008), mortalidade 0,992×, mortes de fome 0,957× (ns),
furtos 1,002×, 0 extinções; custo a 1.000 NPCs: tempo 0,991×, bytes 1,002×. Quase ninguém morre longe levando carga: o
efeito no mundo é pequeno (08c item 155).

## Exercício dos números (AD-182, 08c 155)
A sonda `leavings-exercise` provoca a morte por acidente de um viajante longe de casa com carga a cada 60 dias (16 seeds × 8
anos, ~70 mortes). Na vila de referência **todo** espólio cai a menos de 6 h de casa: `MaxFetchHours` (24 h) nunca limita e
o estrago quase não age (recolhido em ~4 dias). Sem o laço: 74 espólios, 40 recolhidos pela casa (10 na hora), 4 achados, 26
furtados (35%); com `KnownDeadBarrier`: 78, 45, 4, 24 (31%). O furto vem sobretudo de quem passa com fome nas carestias.
`MaxFetchHours` e `MinFetcherAgeYears` não se exercitam sem viagens longas (cenário de duas vilas: pedido de issue no AD-182).

## Números sem fonte
`MaxFetchHours` 24, `TheftChancePm` 250, `SeenBarrierPerWitness` 0,15, `KnownDeadBarrier` 0,5, `GroundSpoilagePm` 3000,
`GroundWaterLossPerDayPm` 20, `GroundQ10` 2 (a regra geral da biologia é 2–3) e `MinFetcherAgeYears` 12 são escolha do modelo,
confiança baixa (precisam de conferência).
