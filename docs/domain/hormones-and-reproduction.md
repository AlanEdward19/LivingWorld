# Hormônios, ciclo de vida e reprodução (AD-139)

O corpo tem hormônios, e eles mudam o que a pessoa faz. Nada roda por tick: a puberdade é
marcada uma vez (crescimento mensal), o resto é lido na hora pela idade, sexo, desnutrição,
gravidez e por sorteios fixos da pessoa (semente + id). Regras em `EndocrineRules`
(`ReproductionRules.Endocrine`; `Disabled` desliga), `IntimacyRules` (`UnionRules.Intimacy`),
`HeritableDisorderRules` (`BodyRules.Disorders`) e `ContraceptionRules`.

## Puberdade e adolescência
- Idade individual (menarca ~15 nelas, ~15,5 neles, dp 1,2); a desnutrição do mês em que
  viria a adia (até 3 anos com fome plena). Antes dela não há ovulação nem fertilidade.
- **Impulso da adolescência** (sistemas duais): zero antes da puberdade, pico 2 anos depois,
  cai em linha até os 25. Soma impulsividade, tira aversão a risco.
- Onde pesa: utilidade de `Socialize` (pares), bônus de continuidade menor (muda de ideia),
  antevisão descontada (consequências); na rotina, parte dos dias de trabalho vira saída com
  os pares; no cortejo, aceita par com menos exigência; no sexo fora da união, mais chance.

## Ciclo menstrual
- Duração individual (~28 ± 2,5 dias), fase própria; recomeça no fim do intervalo infecundo.
- A concepção cai na **ovulação** (dia `ciclo − 14`), um sorteio por ovulação do mês; sem
  ciclo (gravidez, lactação, fome forte ≥ 0,5 de desnutrição, menopausa) não há concepção.
- Cólica que limita (15% das mulheres): nos 2 primeiros dias metade dos dias de trabalho
  vira descanso e, na escolha urgente, `Work` perde e `Idle` ganha (termo `Menstruation`).
  Tensão pré-menstrual (20%): 4 dias de menos vontade de conviver.
- Quem come mal (desnutrição ≥ 0,2) perde saúde a cada ciclo (ferro, anemia).

## Gravidez de risco e pós-parto
- Sexo decidido na concepção pelo cromossomo do pai (AD-130). Gêmeos (×6), mãe abaixo de
  18 anos (×1,5) e desnutrição (até ×2,5) multiplicam a chance de prematuro.
- Tristeza pós-parto em 12% das mães: abatimento que dura meses (pesa como luto: rotina e
  utilidade). O efeito direto no cuidado do bebê ainda não existe.

## Sexo fora da união e a reação
- Casal casado concebe pelo ciclo. Sem cônjuge, só no mês em que há sexo: com o namorado,
  com o amante (caso com um casado) ou com alguém de afeto mútuo; chance da norma × o
  temperamento dos dois (impulsividade, risco, impulso da adolescência).
- Quando a gravidez aparece (~110 dias): se ela já casou, nada (concepção pré-nupcial); senão
  o pai solteiro e livre casa na chance da norma (× lealdade), ou ela é deixada. Evento
  `UnwedPregnancy`; vergonha e medo nela, abandono com o pai, vergonha e raiva nos pais dela.
  O filho nasce onde ela mora: em geral com os avós.

## Testosterona
Curva por idade no homem (pico 20–30), ~8% dela na mulher. Multiplica a chance de violência
do traído na descoberta do caso (`UnionSystem`). Briga e furto ficam para a fatia de violência.

## Doenças genéticas
- Loci anônimos, recessivos ou dominantes, com frequência nos fundadores, mutação nova e
  efeito no afetado (risco de morte desde a idade de início, fertilidade). Portador de
  recessiva sem sintoma. Fundador: genótipo da semente; nascido: um alelo de cada pai.
- Sem regra própria para parentes: o filho de primos tem mais chance de receber o mesmo alelo
  do mesmo avô. Consumidores: mortalidade mensal e concepção.

## Contracepção
Não existe por padrão. Só uma tecnologia da categoria `fertility-control` adotada pela cidade
(o motor de emergência decide se alguém a inventa) protege a usuária (sorteio fixo × norma),
depois dos filhos que ela quer, com a eficácia do vetor e a falha do resto. Hoje nenhuma
categoria assim emerge: o gancho existe, sem conteúdo escrito à mão.

## Números
Fontes, conferência e medidas em `docs/decisions-log.md` (AD-139). Medida de 20 seeds:
`bash scripts/demography-report.sh` (linhas `reproducao`, `menarca`, `genetica_fim`).
