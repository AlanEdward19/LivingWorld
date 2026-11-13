# Pressão da população e freios preventivos (AD-145)

Uma vila pré-industrial não para de crescer só pela fome (o freio positivo de Malthus). O que a segurava antes era o
**freio preventivo**: casava-se tarde, ou não se casava, porque não havia com o que montar casa; e quem não tinha lugar ia
embora. No motor nada disso é teto escrito nem taxa fixa: sai da decisão de cada pessoa sobre o que ela tem.

> **Estado (AD-145): tudo desligado por padrão** (`FamilyRules.Pressure` nulo: hash e snapshot iguais aos de antes).
> Ligado (`PopulationPressureRules.Measured`), 20 seeds × 100 anos
> deram 7 extinções e 13% das mortes por sede (contra 0,1% na base), com causa não isolada. Os mecanismos e os testes
> ficam; ligar por padrão espera a investigação (backlog 08, itens 37 a 43).

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

## O primeiro filho também
O casal sem ninguém trabalhando e com a despensa curta concebe menos o primeiro filho, na média da prudência dos dois
(`FirstBirthMeansWeight`). Depois do primeiro parto, fecundidade natural: o controle dentro do casamento era
fraco na época, e o motor não o inventa.

## A família de origem antes da esmola
`MutualAidSystem`: a casa sem comida e sem dinheiro recebe primeiro dos seus. Pais, **avós** (também idosos) e irmãos
mandam **comida da própria despensa** (o que passa de 14 dias dos moradores deles, uma remessa por casa por dia, mais
para os mais generosos) e moeda da carteira; só o que faltar vai pedir à cidade. Evento `AidGiven` com `|food` no fim
quando é comida. Nada é criado: comida e moeda só mudam de casa.

## Esmola racionada
O tesouro só enche na virada do mês (imposto do salário e dízimo, AD-140). A esmola do dia é repartida entre todos os
pedidos do dia, até o caixa dividido pelos dias que faltam para o mês virar. Antes a primeira casa levava a semana inteira
e o tesouro secava no meio do mês, que é quando o recém-nascido de casal sem trabalho morria.

## Quem não tem lugar vai embora
`EmigrationSystem` (mensal): o adulto de 16 a 35 anos, solteiro, sem filho, sem gravidez, sem trabalho e **sem nenhuma vaga
paga na cidade** decide partir com a chance de `EmigrationRules.DecisionPerYear` (`Pressure.Emigration`), vezes o temperamento (abertura e ambição
empurram, aversão a risco segura) e menos os laços (pais vivos na cidade, o maior afeto por alguém dela).
- Com cidades ligadas e outra cidade com vaga paga, vai para lá (viagem da migração, `RelocationArrivalSystem`).
- Sem nenhuma, **sai do mundo simulado**: deixa o dinheiro com um adulto da casa (ou o herdeiro), a casa perde o morador,
  `Npc.Emigrated` = verdadeiro e `DeathDate` marca a partida. Não é morte: o relatório conta à parte (`emigrantes`).
- Desenho honesto com o motor: a migração de casa inteira (`MigrationSystem`) exige duas cidades e só move a casa pelo
  chefe; não havia como sair do cenário de uma vila só. A agregada (`CityGrowthSystem`) já deixava gente sair do pool.

## Água e compra
Origem medida da água aos milhares: cada ida ao mercado por comida trazia a meta inteira de água por cima do estoque
(comida cara, idas repetidas). Com `TopUpPurchases` a compra completa a meta e só se vai pelo que se pode pagar.

## Números
Sem fonte própria (calibração do modelo): peso dos meios 0,9, economias de 24 meses, 40% dos meios no trabalho, peso do
primeiro filho 0,5, chance de partir 35%/ano, laços seguram até 50%. Referência de checagem (Hajnal 1965; Wrigley e
Schofield): mulher casa pela 1ª vez a ~23–26 anos, 10–20% nunca casam. Medidas em `docs/decisions-log.md` (AD-145).
