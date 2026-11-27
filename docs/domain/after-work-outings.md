# Depois do turno: casa, largo, conhecido, passeio (#242)

A cidade parava porque quem acabava o turno (14 h) ficava à toa ou conversando onde estava, no posto ou na lavoura em volta,
até a hora de dormir: às 15–17 h só 3% dos empregados estavam em casa, 55% à toa no posto ou no terreno, e o passeio era um
passo sorteado a cada 3 h (`AmbientStepsPerHour`). A viagem em metros (AD-197o2) fez a ida durar segundos; faltava a pessoa **ter
aonde ir**. Diagnóstico completo: `.specs/features/realism-backlog/relatorios/cidade-viva-242.md`.

## A escolha do tempo livre (`BehaviorDecisionSystem.Outings.cs`)
Quando a escolha da hora é **À toa** ou **Conversar** (o refino de local, `RefineForLocation`), `OutingDestinationOf` diz aonde ir.
Nada de lugar novo: só o que o motor já tem.
- **Casa** (`Household.Location`), o **largo** (o centro da cidade), o **mercado** mais perto de casa, a **casa de quem a pessoa
  mais gosta** (afeto > 0, mesma cidade; os laços sociais) e um **passeio curto** (3, 5 ou 8 células, 4,5 a 12 m, da casa, numa das oito direções, só em
  terreno alcançável a pé). Tudo a até `SeekCompanyMaxCells` (12) da casa.
- **O peso de cada lugar vem do estado**: cansaço (sono) e fim do dia (19 h) pesam a casa; a falta de gente (necessidade social)
  pesa largo, mercado e conhecido; o passeio só de dia. O sorteio é `world.Rng.DrawFor("outing", pessoa × bloco)`, sem stream
  guardado: a mesma pessoa no mesmo bloco de 6 h (`OutingBlockHours`) escolhe o mesmo lugar, então quem chegou fica.
- **Quem está num dos lugares** (em casa, no largo) fica 50% das vezes até o bloco virar; quem quer conversar e já tem gente por
  perto (colegas do posto, a família) fica 50% das vezes: a conversa não vira viagem.
- Fica onde está: de noite (antes das 6 h e depois das 20 h), criança, quem cuida de doente no dia de cuidado,
  quem está de luto (retraimento social) e **quem tem o trabalho na rotina da hora** (o à toa veio de humor ou corpo: ele não sai).
- A ida atrás de companhia (AD-186, só para quem conversa e está só em casa) vale primeiro; sem ela, a escolha acima.
- A viagem guarda o destino no intento `Socialize` (`PinCompanyTarget`, como a de AD-186) e o motivo da viagem passa a poder ser
  `Idle`; a projeção lê o alvo (`CompanyKey`: `household:`, `city:` ou `site:x,y`). Quem chega de uma viagem por outro motivo (o posto)
  decide de novo.
- Desligada (`StreetRules.AfterWorkOutings = false`, `LW_AD192=nooutings`) vale o de antes: só a ida de AD-186 e o passo a cada 3 h.

## Dentro de casa (item 4)
`BuildingOccupancySystem.Place` recebe a posição do morador entre os da casa (`SleeperRank`, por id): o r-ésimo fica com a r-ésima
**cama** (`HomeFurniture.BedSpots`), passadas as camas, com o piso livre (fora da mobília, da porta e da escada); antes o hash dava a
mesma cama a todos e o snapshot do interior empilhava seis moradores numa célula.

## Placa da cidade (item 6)
`CityDensityVisual.Outdoors` (aditivo, opcional): dos que estão dentro dos limites (`Inside`), os que estão fora da planta do térreo
de qualquer prédio (`BuildingFootprints`, guardado por mundo). O cliente escreve "N na cidade · M na rua · P no pool"; sem o dado
do motor, só "N na cidade".

## Custo e limites
Uma escolha por despertar de À toa/Conversar (memória do tick, como a de AD-186), sem custo por tick de quem viaja. No passo de hora
cheia uma viagem custa ao menos um tick, então o número "viajando" nos relatórios por hora é maior que ao vivo (segundos).
