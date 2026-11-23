# Bairro denso: higiene, como é visto e onde morar (AD-192)

Casas coladas e apinhadas são permitidas (com acesso sempre: viela, escada, laje, telhado, [city-layout](city-layout.md)). O que
elas mudam no mundo sai do que o motor já mede, sem lista de bairros nem rótulo fixo. Código: `Cities/Quarter.cs`.

## A sujeira da casa (0–1)
Média ponderada (pesos em `StreetRules`, somam 1):
- **Apinhamento** (0,3): gente de todas as famílias do prédio por andar, acima de `CrowdPersonsPerFloor` (5); o dobro = 1.
- **Paredes coladas** (0,3): parte da volta da casa (as células vizinhas da planta) tomada por outro prédio, pela grade de obstáculos
  da rota (a mesma que decide escada, laje e viela).
- **Escoamento** (0,2): sem quintal que absorva 0,5; + 0,5 × a parte da volta mais alta que a casa (a água da chuva e o despejo
  correm para baixo); calçamento (cascalho, pedra) na soleira corta pela metade.
- **Água** (0,2): metade pela ida a pé à fonte (`WaterFetch.SourceFor`, cheia a partir de `SqualorWaterFarHours` = 2 h: a água vai
  toda para beber), metade pela fonte cercada de casas (`SourceCrowdHouses` = 8 a até 3 células: quem tira e quem despeja no mesmo
  lugar). Sem fonte da natureza ao alcance, 0,5.
A parte do lugar (sem o apinhamento) fica num cache refeito quando a grade da rota muda; o apinhamento é lido na hora.
Medido no começo (seed 1): mediana 0,11–0,12 nos quatro mundos, p90 0,14–0,21, máximo 0,33 (metrópole); nenhum bairro denso
emergiu ainda nos mundos de hoje (o 09b item 5, o bairro que nasce da terra cara, é outra fatia).

## Higiene → doença (`OutbreakSystem`)
- **Casos do ambiente** das doenças de água e comida e de vetor (diarreia, febre): cada candidato é aceito também pelo fator da casa
  dele, 1 + `HygieneGain` (2) × (sujeira − `HygieneReference`), entre 0,6 e 2,5. A referência (0,12) é a mediana da vila: as taxas da
  [doença](disease.md) foram calibradas para a vila típica, então só a casa mais suja (ou a mais limpa) se afasta delas. Sem casa: 1.
- **Parede-meia** (respiração e contato): quem está contagioso encontra os moradores das casas que encostam na dele e das outras
  famílias do mesmo prédio, `WallContactsPerDay` (2) × (vizinhas + o próprio prédio, até 4)/4 encontros por dia, à taxa do encontro na
  vila. Casa solta sem ninguém mais dentro: nada muda.

## Como o bairro é visto, por pessoa
Aversão de −1 (curiosidade) a +1 (medo e nojo): quem nasceu no limite da cidade parte de 0,4, quem veio de fora de −0,2; aversão ao
risco e pouca estabilidade somam, abertura tira (0,25 por 50 pontos de cada traço); morar na sujeira acostuma (− 0,8 × a sujeira da
própria casa); cada crime que a pessoa conhece na cidade (vítima ou boato) de quem ela acredita ser o autor e que mora em casa de
sujeira ≥ `DenseSqualor` (0,35) soma 0,25, até 0,75. Mediana medida: 0,34–0,38; p10 perto de 0 (os de fora, abertos).

## O que a aversão decide
- **Onde morar** (`BuildWorks`, lote da casa nova e casa vazia, AD-184): custo por ano += aversão média dos adultos (sem a
  curiosidade) × `QuarterDaysPerYear` (20) dias de jornal por morador × a sujeira do lugar; e a sujeira de onde mora agora (de favor
  na casa cheia) pesa a favor de mudar.
- **Valor** (`BuildingValuation`): fator de local − `QuarterValuePenalty` (0,4) × sujeira do lugar; o aluguel e a diária saem dele.
- **Migração** (`MigrationSystem`): a segurança da cidade deixa de ser 0,5 fixo: 0,5 × (1 − medo), medo = crimes que a casa conhece
  lá (0,25 cada, por morador) + aversão × a parte suja da cidade.
- **Visita de fora** (turismo da favela): não há viagem de lazer entre cidades no motor; fica como issue (relatório da AD-192).

## Chaves (todas ligadas; `LW_AD192=off` desliga a fatia; `nohygiene`, `noregard`, `noleft` uma parte)
`QuarterHygiene`, `WallContagion`, `QuarterInLotChoice`, `QuarterInValue`, `QuarterInMigration` (em `StreetRules`).

## Fontes e confiança
- Cortiço e pátio apinhado com mais doença e morte: Chadwick, *Report on the Sanitary Condition of the Labouring Population*
  (1842); bomba d'água cercada de fossas: Snow, *On the Mode of Communication of Cholera* (1855) — alta no mecanismo, nenhuma no
  número. Ganho 2, referência 0,12, piso 0,6, teto 2,5, pesos 0,3/0,3/0,2/0,2: escolhidos, precisam de conferência.
- Medo e estigma do bairro pobre pelos de perto, curiosidade dos de fora (o turismo de favela): Freire-Medeiros, *Touring Poverty*
  (2013) — média, qualitativo. Todos os números da aversão: escolhidos.
