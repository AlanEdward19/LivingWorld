# Pressão da população e freios preventivos (AD-145)

Uma vila pré-industrial não para de crescer só pela fome (o freio positivo de Malthus). O que a segurava antes era o
**freio preventivo**: casava-se tarde, ou não se casava, porque não havia com o que montar casa; e quem não tinha lugar ia
embora. No motor nada disso é teto escrito nem taxa fixa: sai da decisão de cada pessoa sobre o que ela tem.

> **Estado (AD-148): ligado no cenário de código** (`ScenarioRunner.DefaultFamilyRules`, `Pressure = Measured`); cenário
> JSON sem o bloco segue desligado. No AD-145 ligado deu 7 extinções (sede), no AD-146 3 extinções e fome 11,9%; a causa era
> o trabalho e a compra da casa ([household-provisioning.md](household-provisioning.md)). Medir desligado:
> `LW_PRESSURE=off`; só algumas chaves: `LW_PRESSURE=topup,kin,alms,means,first,emig`.
> **AD-149**: a terra é finita e a emigração passou a disparar ([land-and-harvest.md](land-and-harvest.md)): antes qualquer vaga
> aberta da cidade (a forja de 40 vagas) segurava o jovem, e 20 × 100 anos tiveram 0 emigrantes.

## Casar depende de meios
Todas as chaves estão em `PopulationPressureRules` (`FamilyRules.Pressure`).
- No cortejo (`CourtshipSystem`), além de querer o outro, cada um pesa os **meios do casal** (`HouseholdMeans.OfCouple`):
  alguém dos dois com trabalho (`MarriageEarnerShare`) e as economias dos dois contra
  `MarriageSavingsMonths` meses de comida e água de uma casa de dois.
- O peso vem da **prudência** de quem decide (`HouseholdMeans.Prudence`): consciência e aversão a risco contra
  impulsividade, com o impulso da adolescência e a antevisão descontada dele (AD-139), e a lembrança de fome (AD-138), que
  deixa mais cauteloso. O jovem impulsivo casa sem meios; o prudente espera trabalho e economias.
- Recusa nomeada: `CourtshipRejected` com `SemMeios`. A gravidez fora da união que obriga a casar passa por cima (AD-139).
- Consequência medida: idade ao primeiro casamento e parte de quem nunca casa sobem quando faltam vagas
  (`demography-report`, linha `primeiro_casamento`).

## Só casa quem tem do que viver (AD-158)
Os meios acima quase nunca seguravam (moeda farta nas carteiras, ~90% dos noivos empregados): a mulher casava aos ~17,8 e
quase ninguém ficava solteiro. A peça que faltava era o **nicho**: na aldeia o casal só montava casa com uma posse de terra,
um ofício ou a casa de quem morreu (`HoldingRules`, `Pressure.Holdings`; `Holdings` no motor; nenhum estado novo).
- Terra demarcada (`CropBatch.ArableSeed`, AD-149) menos a **terra em uso**: cada morador de família que vive da terra
  (ninguém dela em ofício fora da lavoura) come `PersonSownHectares` (0,7 ha semeados); a família com casal em idade fértil
  guarda uma posse inteira (`HoldingSownHectares`, 3,5 ha: os filhos que virão). Sobrando uma posse, há **posse vaga**. A
  casa de velhos ou de viúva que encolhe devolve a terra; a que se desfaz, toda.
- O casal tem sustento se um dos dois tem **ofício fora da lavoura com salário**, ou **chefia a própria casa** e sai dela
  levando só os filhos, ou há posse vaga. Sem nenhum dos três, nem o impulsivo casa (`MarriageReadiness` = 0); no fim do
  namoro a vaga pode ter ido a outro casal e o namoro acaba (`CourtshipRejected|SemMeios`). A união forçada pela gravidez
  passa por cima e vive apertada.
- Consequência: com a terra cheia o casamento espera a morte dos velhos, e quem não tem trabalho parte (emigração).
- Tentativas descartadas (6 seeds): casa vaga ou construída com as economias não mudou nada (o casal sempre tinha a
  moeda); posse fixa por família (contagem) fazia a vila encolher (viúva sozinha segurava uma posse inteira).
- Sobra medida: a mulher casa aos ~21 com o homem de ~28 (cortejo aceita o primeiro par aceitável; quem tem posse escolhe
  menina nova) e quase nenhuma mulher fica solteira; o celibato cai nos homens. Medir sem a regra: `LW_PRESSURE=nohouse`.

## O primeiro filho também
O casal sem ninguém trabalhando e com a despensa curta concebe menos o primeiro filho, na média da prudência dos dois
(`FirstBirthMeansWeight`). Depois do primeiro parto, fecundidade natural: o controle dentro do casamento era
fraco na época, e o motor não o inventa. AD-182 (08c 151): com o ciclo hormonal a fome que impede conceber é a do corpo
(amenorreia da subnutrição); o piso de 5 de comida em casa (`ConceptionResourceFloor`) deixou de valer
(`FamilyRules.ConceptionFloorReadsBody`). Era ele, e não `FirstBirthMeansWeight` (que nunca segurou nada: quase todo casal tem
trabalho), que tirava filhos da casa que mandava comida a parente.

## A família de origem antes da esmola
`MutualAidSystem`: a casa sem comida e sem dinheiro recebe primeiro dos seus. Pais, **avós** (também idosos) e irmãos
mandam **comida da própria despensa** e moeda da carteira; só o que faltar vai pedir à cidade. Evento `AidGiven` com
`|food` no fim quando é comida. Nada é criado: comida e moeda só mudam de casa. AD-190: sem os seus, as 6 casas mais perto na
cidade (`NeighbourAidHouses`) decidem pelo altruísmo (× 0,4, sem laço) mandar comida acima da reserva (`|food|vizinho`).
AD-166 (quebra de colheita, 08c item 148): a moeda só tira a casa da ajuda se compra comida (`AidSeesFoodForSale`: um
mercado a até 4 h com comida à venda e a casa não acabou de achar a banca vazia); sem isso a casa com moeda também é
socorrida, em comida. A comida vem de parente **ou gente de afeto**, acima de 2 dias por morador da casa de quem dá
(`FoodAidReserveDays`, a meta da compra; antes 14 dias, que despensa nenhuma tinha: a ajuda em comida era zero). Seed 14,
12 meses de quebra: furtos 448 → 410 (carga desligada 394), banidos 35 → 27 (28), vivos no ano 6: 55 → 63 (64).
40 × 15 pareado: fome crítica 1,055× (IC90 0,90–1,20), mortes de fome 0,88×, mortalidade 0,99×, natalidade 0,979×
(0,966–0,992: quem dá fica com a despensa curta, que adia o primeiro filho). Só a moeda que vê a banca, sem a comida
do excedente, sobe a fome (1,03×; cortar também a moeda e a esmola nesse caso dava 1,10×, revertido). AD-182: moeda na mão de
quem não sabe comprar (o marco de comprar do desenvolvimento) também não alimenta (`AidSeesWhoCanBuy`): a casa é socorrida em comida.
AD-191 (`PurseToShopper`): a bolsa da casa vai a quem compra (o neto que ganha e não tem o marco entrega ao avô) e a ajuda só
conta a moeda de quem compra; antes o avô sem moeda morria de fome ao lado da banca (seed 22). Furto de comida e água vai à
justiça pelo preço de costume, não pelo da carestia (`JusticeRules.CustomaryValue`).

## Esmola racionada
O tesouro só enche na virada do mês (imposto do salário e dízimo, AD-140). A esmola do dia é repartida entre todos os
pedidos do dia, até o caixa dividido pelos dias que faltam para o mês virar. Antes a primeira casa levava a semana inteira
e o tesouro secava no meio do mês, que é quando o recém-nascido de casal sem trabalho morria.

## Quem não tem lugar vai embora
`EmigrationSystem` (mensal): o adulto de 16 a 35 anos, solteiro, sem filho, sem gravidez, sem trabalho e **sem vaga que o
contrataria na cidade** (ofício dele, folha paga, encomenda ou terra pedindo a mão; AD-149) decide partir com a chance de `EmigrationRules.DecisionPerYear` (`Pressure.Emigration`), vezes o temperamento (abertura e ambição
empurram, aversão a risco segura) e menos os laços (pais vivos na cidade, o maior afeto por alguém dela).
- Com cidades ligadas e outra cidade com vaga paga, vai para lá (viagem da migração, `RelocationArrivalSystem`; AD-190: a mudança se completa quando o chefe chega, e quem vem atrás segue para a casa nova).
- Sem nenhuma, **sai do mundo simulado**: deixa o dinheiro com um adulto da casa (ou o herdeiro), a casa perde o morador,
  `Npc.Emigrated` = verdadeiro e `DeathDate` marca a partida. Não é morte: o relatório conta à parte (`emigrantes`).
- Desenho honesto com o motor: a migração de casa inteira (`MigrationSystem`) exige duas cidades e só move a casa pelo
  chefe; não havia como sair do cenário de uma vila só. A agregada (`CityGrowthSystem`) já deixava gente sair do pool.

## Água e compra
Origem medida da água aos milhares: cada ida ao mercado por comida trazia a meta inteira de água por cima do estoque
(comida cara, idas repetidas). Com `TopUpPurchases` a compra completa a meta e só se vai pelo que se pode pagar.

**Sede com água em casa (AD-146)**: era o `TopUpPurchases` sozinho. Sem moeda para a comida, a pessoa ficava no mercado
comprando a água que completava a meta (uma ou duas por ida); a compra "dava certo", o plano de comida terminava sem
beber, e de duas em duas horas ela comprava de novo até morrer de sede. Corrigido na raiz, valendo com ou sem os freios:
quem fecha a compra com fome ou sede crítica come e bebe do que a casa tem.

## Números
Sem fonte própria (calibração do modelo): peso dos meios 0,9, economias de 24 meses, 40% dos meios no trabalho, peso do
primeiro filho 0,5, chance de partir 35%/ano, laços seguram até 50%. Referência de checagem (Hajnal 1965; Wrigley e
Schofield): mulher casa pela 1ª vez a ~23–26 anos, 10–20% nunca casam. Posse de 3,5 ha e 0,7 ha por pessoa: calibração
do modelo (meia virgata inglesa ~6 ha, 2/3 semeados, precisa de conferência). Medidas em `docs/decisions-log.md` (AD-145, AD-158).
