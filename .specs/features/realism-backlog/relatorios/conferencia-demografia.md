# Conferência dos números demográficos (AD-130) contra a literatura

Código lido no branch `perf/decision-scan-costs`. Grau de evidência de cada fonte:
**[L]** li o trecho na página aberta · **[B]** só no resumo do buscador (a página não abriu: 403, PDF ilegível) ·
**[C]** calculado por mim · **[M]** lembrança minha, sem conferir.

**[C] Coale-Demeny Oeste, feminina:** portei para Python os coeficientes de `demogR::cdmltw()`
(raw.githubusercontent.com/cran/demogR/master/R/cdmltw.R, código lido). A porta reproduz e0 = 25,0 / 27,5 / 30,0 / 32,5 / 35,0
nos níveis 3 a 7, o que a valida. Taxas anuais = 1 − (1 − nqx)^(1/n). Rodei também a tábua `PreIndustrial` sozinha
(sem saúde, vitalidade nem prematuridade): e0 = 33,0, l15 = 0,577, 4q1 = 0,185, 5q5 = 0,077.

| # | Item | Código (arquivo:linha) | Literatura | Fonte | Veredito | Recomendado |
|---|---|---|---|---|---|---|
| 1 | Mortalidade no 1º ano | 0,20 (`LifeTable.cs:92`) | West: 0,256 com e0 30, 0,234 com 32,5, 0,214 com 35. Inglaterra, média nacional de meados do séc. XVIII: ~180‰. Volk & Atkinson, amostra histórica: 27%. Nobreza inglesa do fim da Idade Média: ~1/3 | [C]; Inglaterra [B] (resenha citada na busca); Volk [L] OWID *child-mortality-in-the-past*; nobreza [L] Wikipedia *Life expectancy* | dentro (faixa de 0,15 a 0,30) | manter 0,20 (ou 0,22 para e0 ≈ 31) |
| 2 | Mortalidade de 1 a 4 anos | 0,05/ano, 4q1 = 0,185 (`:93`) | West: 4q1 = 0,178 (0,048/ano) com e0 30, 0,161 com 32,5 | [C] | dentro | manter |
| 3 | Mortalidade de 5 a 9 anos | 0,016/ano, 5q5 = 0,077 (`:94`) | West: 0,0103/ano com e0 30; 0,0124/ano mesmo com e0 25 | [C] | **alto** (acima até do nível de e0 25) | **0,010** |
| 3b | Mortalidade de 10 a 14 anos | 0,008 (`:95`) | West com e0 30: 0,0080 | [C] | dentro | manter |
| 4 | Adulto | 0,011 / 0,013 / 0,017 (20–29 / 30–39 / 40–49, `:97-99`) | West com e0 30: 0,013 / 0,0167 / 0,0197; com e0 35: 0,011 / 0,014 / 0,017 | [C] | dentro (o código está no nível de e0 35) | manter, ou ~0,012 / 0,015 / 0,018 para casar com e0 32 |
| 5 | 60–64 anos | 0,045 (`:102`) | West: 0,053 com e0 30, 0,045 com e0 35 | [C] | dentro | manter |
| 6 | 80–84 anos | 0,20 (`:106`) | West: 0,212 com e0 30, 0,194 com e0 35 | [C] | dentro | manter |
| 7 | e0 medida: 31,9 | resultado (AD-130) | Inglaterra moderna: 33 a 40; séc. XVIII: 25 a 40; Europa no início do séc. XIX: ~33; nobreza no fim da Idade Média: 30 a 33. Em 1800, nenhuma região passava de 40 | [L] Wikipedia *Life expectancy*; [L] OWID *life-expectancy* | dentro | — |
| 8 | 216‰ no 1º ano (medido) | resultado | igual ao item 1 | idem | dentro | — |
| 9 | 56% chegam aos 15 anos (medido) | resultado | Suécia 1750–80: 60%. Baviera: ~50%. Volk & Atkinson: ~metade. West: l15 = 0,558 com e0 30 | [L] OWID; [C] | dentro | — |
| 10 | Natalidade 36,8‰, mortalidade 30,9‰ | resultado | No estágio 1, natalidade e mortalidade "muitas vezes acima de 40‰" (fonte fraca). Faixa clássica de 30 a 40‰ | [L] Wikipedia *Demographic transition*; faixa [M] (Livi-Bacci) | dentro, sem fonte primária. O crescimento de ~0,6%/ano é alto para uma vila no longo prazo, mas a Inglaterra do séc. XVIII chegou a esse nível [M] | — |
| 11 | 5,0 filhos por mulher | resultado | Algumas regiões da França no séc. XVII ficavam abaixo de 5; máximo observado ~10. Casadas na Baviera: 5,6 | [L] Leridon, encyclopedia.com *Natural fertility*; [L] OWID (Knodel) | dentro (no limite baixo) | — |
| 12 | Intervalo entre partos: média 41, mediana 34 meses | resultado | Inglaterra pré-transição: média de 924 dias (~30 meses), dp 455 dias, em 15.845 famílias (Cinnirella, Klemp & Weisdorf 2017, *Demography*). Bongaarts: intervalo ≈ 18,5 + i meses (i = infecundidade pós-parto) | Inglaterra [B] (PDF recusou conexão); fórmula [L] no PDF de Bongaarts 2015, *Demographic Research* 33(19), índice Ci = 20/(18,5+i) | **alto** (média 41 contra ~30) | ver item 14 |
| 13 | Razão de sexo ao nascer | 105/205 (`ReproductionRules.cs:43`); medida 104,6 | ~105 (103–107, OMS) | [L] OWID *gender-ratio* | dentro | manter |
| 14 | Amenorreia pós-parto | 480 ± 150 d, ~15,8 meses (`:55`) | Bongaarts–Potter: PPA = 1,753·exp(0,1396·BF − 0,001872·BF²). Amamentar 18 meses dá ~11,8 de amenorreia; 24 meses dá ~17,0 | fórmula [B] (snippet do HAL; o documento negou acesso); contas [C] | **alto** para a Europa (interv. 18,5 + 15,8 ≈ 34 meses contra ~30 observados) | **365 ± 120 d** (~12 meses; dá intervalo ≈ 30,5 meses) |
| 15 | Fecundabilidade mensal no pico | 0,20 (`:38`) | "no máximo 20–25% em média" (Leridon, INED); efetiva (a que leva a nascido vivo) de 20 a 30% no 1º ciclo (Leridon 1990) | [B] (dois resumos de busca) | dentro. A perda de ~10% aplicada depois deixa a efetiva em ~0,18 | manter 0,20, ou 0,22 se o intervalo cair demais |
| 16 | Fração estéril | 3/16/31/69/100% aos 20/35/40/45/50 (`:41`) | Tabela "sem nascido vivo pela idade de início" em 7 estudos: aos 20, 2,2–4%; aos 35, 13–22%; aos 40, 24–46%; aos 45, 50–79%. Leridon 1977: 3/6/10/17/29/50. Menken-Larsen 1986: 4/7/12/22/46/– | [L] Wikipedia *Age and female fertility* | dentro (aos 45 está no alto; a mediana das colunas é ~58%) | manter, ou 0,60 aos 45 |
| 17 | Fecundidade 40–44 / 20–24 | 0,30 (medido pelo agente) | Padrão natural n(a) de Coale-Trussell: 0,167/0,460 = 0,36 | [M] (tabela não aberta: timeouts) | provavelmente dentro, levemente baixo | sem fonte; conferir n(a) em Coale & Trussell 1974 |
| 18 | Gestação a termo | 268 ± 9 d da concepção, 245–295 (`:47`) | Mediana de 268 dias da ovulação ao parto; amplitude de 37 dias nos partos a termo (Jukic et al. 2013, *Hum Reprod*) | [B] | dentro | manter |
| 19 | Prematuros (% dos partos) | 0,5 / 1,2 / 5,8 = 7,5% (`:50-52`) | Prematuridade de 4 a 16% dos partos (OMS 2020). Divisão de ~5% / 10% / 84% dos prematuros (Blencowe 2012) | OMS [L]; divisão [M] (só confirmei as categorias, não os %) | total dentro. Muito prematuro: 16% dos prematuros, alto contra ~10% [M] | 0,4 / **0,75** / 6,35 |
| 20 | Morte no 1º mês dos prematuros | 90% / 50% / 12% | OMS, baixa renda: "mais de 90%" dos <28 sem morrem; "metade" dos nascidos com ≤32 sem morre | [L] OMS *preterm-birth* | <28 e 28–31: dentro. 32–36: **sem fonte encontrada** | manter |
| 21 | Perda gestacional | 0,10–0,11 aos 20–29; 0,33 aos 40; 0,55 aos 45; 0,70 aos 50 (`:58-59`) | Dinamarca 1978–92, abortos com internação: 8,9% aos 20–24 e 74,7% aos 45+. Perda fetal: 54,5% aos 42. Se só 80% dos abortos internarem: 11,1 / 11,9 / 15,0 / 24,6 / 51,0 / 93,4% (20–24 … 45+) | [L] Nybo Andersen et al. 2000, BMJ (PMC27416) | 20–39: dentro. **40–45: baixo** (o código dá ~0,42 aos 42, contra 0,545) | 40: **0,40**; 42: **0,55**; 45: **0,75**; 50: 0,85 |
| 22 | Morte materna-base | 1% por parto × idade × paridade × saúde (`ScenarioRunner.cs:109`, `NatalitySystem.cs:143`) | Inglaterra pré-industrial, 1650–1800: média de 10 por 1.000 partos (Alter, a partir de Schofield). Risco ao longo da vida: 6–7% (Schofield). Fim do séc. XVIII: 5 a 29‰ (Loudon) | [B] (três resumos de busca; páginas com 403) | a base está na faixa, mas o **efetivo fica acima**: o multiplicador médio é >1 (saúde ≥1, 1º parto 1,3) | calibrar para efetivo ≈ **10‰** medido (base ~0,007 se o multiplicador médio for ~1,4) |
| 23 | Natimorto-base | 3% × multiplicadores (`ScenarioRunner.cs:110`) | Inglaterra 1600–1930: 40 a 60 por 1.000 partos (Woods 2009, *Death before Birth*) | [B] | dentro (efetivo ~4%) | manter; conferir se o medido fica entre 40 e 60‰ |
| 24 | Risco por idade e paridade | 3,0 aos 12; 2,0 aos 15; 1,2 aos 18; 1,4 aos 35; 2,0 aos 40; 3,0 aos 45; 1,3 no 1º parto; 2,0 com 12 partos (`:61-62`) | 144 países: 260 contra 190 por 100 mil entre 15–19 e 20–24 (≈1,4×). Curva em J, com o maior risco depois dos 30 | [B] Nove et al. 2014, *Lancet Glob Health* | 15–19: dentro. <15, 35+, 1º parto e paridade alta: **sem fonte encontrada** (direção correta) | manter |
| 25 | Gêmeos | 1,2% dos partos; 30% idênticos (`:44-45`) | Europa pré-industrial: 9 a 17 por 1.000 maternidades. Monozigóticos: 3,5–4 por 1.000, estável (Bulmer 1970), o que dá 25–40% dos pares | [B] | dentro | manter |
| 26 | Morte por falta de água | sede de 100 a 0 em 33 h, mais 34 h em zero ≈ **2,8 dias** (`NeedsDecaySystem.cs:51`) | "mais perto de uns 3 dias" (Yamada 2022); casos extremos de 8 a 21 dias | [L] Healthline | dentro | manter |
| 27 | Morte por falta de comida | 21 dias com a fome em zero (`NeedsRules.cs:27`) | "quase três meses" em repouso; "dois meses é aposta segura". Grevistas de fome morreram em 45 a 61 dias | [L] The Conversation (Broer 2021); grevistas [B] | **baixo** | **45 dias** (1.080 h); ~30 dias se a desnutrição prévia e o trabalho pesado forem modelados à parte |
| 28 | Morte por privação de sono | sono de 100 a 0 em 67 h, depois −2 de saúde/h; com saúde 100, morre em ~5 dias (7–8 segundo o agente) | Nenhum caso conhecido de morte humana só por privação de sono (Gillin). Ratos morrem com ~2 semanas ou mais. Extrapolação para humanos: ~77 dias | [L] Scientific American 2002; 77 dias [B] (relatório DTIC) | **sem respaldo** (mata cedo demais) | não matar por sono; usar dano de saúde com piso, ou risco de acidente/erro. No mínimo, morte só depois de ≥14 dias |

## O que mudar no código (do mais ao menos importante)

1. **Amenorreia pós-parto** (`ReproductionRules.cs:55`): 480 ± 150 → **365 ± 120 dias**. Hoje o intervalo entre partos dá média de
   41 e mediana de 34 meses, contra ~30 meses na Inglaterra (924 d). Remedir o intervalo e os filhos por mulher, que devem ficar em ~5–6.
2. **Morte por fome** (`NeedsRules.cs:27`): 21 → **45 dias** (`45 * 24`). Os dados de grevistas e de jejum ficam entre 45 e 60 dias ou mais.
3. **Morte por privação de sono** (`NeedsDecaySystem.cs:58-66`): tirar a morte, ou adiá-la para depois de **14 dias** ou mais. Não há
   registro de morte humana só por isso.
4. **Mortalidade de 5 a 9 anos** (`LifeTable.cs:94`): 0,016 → **0,010** (Coale-Demeny Oeste com e0 30). Sobe a e0 e a fração que chega
   aos 15; para manter e0 ≈ 32, subir 1q0 para 0,22 ou o adulto para ~0,012 / 0,015 / 0,018.
5. **Perda gestacional depois dos 40** (`ReproductionRules.cs:58-59`): (40, **0,40**), (42, **0,55**), (45, **0,75**), (50, **0,85**).
   Nybo Andersen dá 54,5% de perda fetal aos 42 e 74,7% de aborto espontâneo aos 45+.
6. **Morte materna efetiva**: medir mortes maternas por 1.000 partos e calibrar `maternalDeathRisk` (`ScenarioRunner.cs:109`) para
   ~**10‰** efetivos (provavelmente base ~0,007). Medir também o natimorto efetivo, que deve ficar entre 40 e 60‰.
7. **Divisão dos prematuros** (`ReproductionRules.cs:50-52`): 0,005 / 0,012 / 0,058 → **0,004 / 0,0075 / 0,0635**. O total de 7,5% não
   muda. A divisão de ~5/10/84% dos prematuros é lembrança (Blencowe 2012), por isso a prioridade é baixa.
8. Opcional: fração estéril aos 45 de 0,69 → 0,60 (mediana dos 7 estudos).

**Sem fonte encontrada:** morte neonatal de 12% aos 32–36 semanas sem medicina; fatores de risco obstétrico abaixo dos 15 anos, depois
dos 35, no 1º parto e com paridade alta; razão 40–44 / 20–24 (só lembrança, ~0,36). Itens marcados [B] e [M] merecem leitura do original
antes de virarem "conferido": Wrigley et al. 1997, Schofield 1986, Woods 2009, Blencowe 2012, Coale & Trussell 1974.
