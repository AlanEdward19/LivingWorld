# Conferência em fonte: família, nupcialidade, parto e hormônios (AD-161)

Código lido em `perf/decision-scan-costs` (`a428cd11`). Itens de [08](../08-a-investigar.md)/[08c](../08c-a-investigar.md).
Grau de evidência, como em [conferencia-demografia](conferencia-demografia.md): **[L]** li o trecho na página aberta ·
**[B]** só no resumo do buscador ou em fonte secundária (a página não abriu ou era PDF ilegível) · **[C]** calculado por mim ·
**[M]** lembrança, sem conferir. Nenhum número do código foi mudado; o que está fora da faixa vai a
[correcoes-sugeridas](correcoes-sugeridas.md).

## Parto e bebê sem mãe (itens 3, 6)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 3a | Natimorto quando a mãe morre no parto: 30% | `LactationRules.StillbirthRiskWhenMotherDies` = 0,3 (`Domain/Health/PhysiologyRules.cs:125`) | Saleem et al. 2014, *Bull WHO* 92(8):605–612 (coorte de 214 mil gestantes em 7 sítios de renda baixa e média): risco de natimorto **9,48×** maior quando a mãe morreu; ~70% dos natimortos eram intraparto. Com natimorto-base de ~2,9% nos sítios (6.213 em ~214 mil), 9,48 × 2,9% ≈ **27%** | [B] (abstract; tabela não abriu: reCAPTCHA no PMC, 403 no SciELO); conta [C] | **confere** (medicina moderna parcial; sem medicina, provavelmente mais) |
| 3b | Desgaste de saúde do parto: 12 × risco obstétrico | `PhysiologyRules.DeliveryHealthLoss` = 12 (`PhysiologyRules.cs:52`) | Nada achado que dê "pontos de saúde" por parto | — | **sem fonte encontrada — é escolha de modelo** |
| 6 | Bebê sem mãe sobrevive 0,39 × o com mãe (30% contra 76%); alvo 0,5 | `LactationRules.UnnursedHazardByAgeMonths` 4,5 → 1 (0 a 24 meses, `PhysiologyRules.cs:121`) | Estudos de registro paroquial confirmam que a morte da mãe é o maior risco do lactente (Espanha rural 1750–1959, CPoS; Estônia 1826–1891; Hallstatt 1630–1908), mas **nenhum número de sobrevivência foi lido**: Knodel 1988 não abriu, o PDF de Hallstatt falhou (DNS) | [B] direção | **não conferido** (fica aberto; o alvo 0,5 da auditoria também não tem fonte) |

## Paternidade e vida conjugal (itens 10, 11)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 10 | Paternidade fora do casamento medida 0,2% dos nascidos | `UnionRules.AffairPerYear` 0,05, `AffairPaternityShare` 0,5 (`Domain/Population/Family/UnionRules.cs:43,52`) | Larmuseau et al. 2019, *Current Biology* (513 pares de homens ligados por genealogia, Holanda e Flandres, 500 anos): média ~1–1,6% por geração; ~1% entre lavradores e artesãos remediados, ~4% entre jornaleiros e tecelões, mais alto com densidade. Outro estudo holandês (*Am J Hum Biol*) dá 0,96% | [B] (resumos de GenomeWeb e de notícia; artigo não aberto) | **fora da faixa — sugerido ~1% dos nascidos** (vila de lavradores) |
| 11 | Caso 0,05/ano, fim 0,08/mês, descoberta 0,04/mês + 0,03 por confidente, mágoa 45, separação 0,2, violência 0,1 | `UnionRules` (`UnionRules.cs:43-69`) | Nenhuma taxa histórica de adultério, descoberta ou briga achada. Única restrição externa: a paternidade do item 10, que sai do produto caso × concepção × `AffairPaternityShare` | — | **sem fonte encontrada — é escolha de modelo**; calibrar pelo item 10 |

## Nupcialidade e fecundidade conjugal (itens 120, 121, 123, 84)

| # | Número | Código / medida | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 120a | Alvo: mulher casa aos 23–26 | medida 20×100 (AD-158): ~21; homem 24–27 | Inglaterra: média 25 (mulher) e 27 (homem) no fim do séc. XVI (Cressy); Canterbury 1619–1660: 24 e quase 28 (Laslett 1965); Holanda: 27 e 30 | [L] Wikipedia *Western European marriage pattern* (secundária) | alvo **confere**; o modelo está **fora** (~3 anos cedo) |
| 120b | Alvo: 10–20% nunca casam | medida: mulheres ~0%, homens 1–3% aos 45 | Inglaterra: de <10% para quase 20% das mulheres em meados do séc. XVII (Cressy); Hajnal 1965: 20–30% das mulheres na Europa ocidental | [L] idem | alvo **confere**; modelo **fora** |
| 120c | Diferença de idade procurada: 3 anos | `UnionRules.PreferredAgeGapYears` = 3 (`UnionRules.cs:97`) | Canterbury 1619–1660: medianas 22 a 9 m e 25 a 6 m (≈3 anos, conta minha); o padrão ocidental tem diferença "pequena" | [L] + [C] | **confere** |
| 121a | TFR conjugal 20–49 estimada ~8–9 | linha `por_decada_nupcialidade` (AD-158) | Reconstituição de 26 paróquias inglesas (Cambridge Group): **TMFR 20–49 ≈ 6,7** na amostra de referência (6,8 aceitando meses de nascimento incertos) — Alter, Newton & Oeppen 2020, *Historical Life Course Studies* 9:24–48 | [L] (PMC10923557) | **fora da faixa — sugerido ~6,7–7** |
| 121b | Intervalo entre partos média 29–30, mediana ~26 meses | medida | Inglaterra pré-transição: média 924 d (~30 meses) (Cinnirella, Klemp & Weisdorf 2017, já no relatório de demografia) | [B] | média **confere**; a mediana curta pela morte do bebê é mecanismo real |
| 123a | Primeiros filhos concebidos antes da união: 39% (antes 27%) | medida 20×100 | "Entre 20 e 40%" dos primeiros nascidos no casamento antes de meados do séc. XIX (Reid, Garrett & Jaadla, Campop, 2024); 31–33% no fim do séc. XVI e 37% no meio século até 1849 (Continuity and Change, resumo); paróquias de 13% e 34% (Hair 1970) | [L] Campop; [B] o resto | **confere** (no teto da faixa) |
| 123b | Nascidos fora de união: 2,2% | medida | "Menos de 10%, muitas vezes menos de 5%" dos nascimentos até o fim do séc. XX (Campop 2024) | [L] | **confere** |
| 84a | Separação por violência era possível? | mágoa conjugal +20 por agressão (`DomesticViolenceRules.SpouseGrievance`, `Domain/Crime/PersonCrimeRules.cs:33`) alimenta a separação do AD-136 | Separação "de mesa e leito" (sem novo casamento) nas cortes da Igreja por crueldade (*saevitia*) ou adultério; a mulher precisava provar violência **excessiva**; ações eram raras e Helmholz sugere que os casais se separavam por acordo (Helmholz 1974, citado) | [B] (resumos: History Workshop, medievalists.net, Continuity and Change) | mecanismo **confere**; frequência: a separação por crueldade deve ser **rara** |
| 84b | Natalidade 5% menor pela separação | medida (6 seeds) | Nada achado | — | **sem fonte encontrada** |

## Hormônios (item 29) — `EndocrineRules` (`Domain/Population/Body/EndocrineRules.cs`)

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| Menarca média 15,0, dp 1,2 | `FemalePubertyMeanYears`, `PubertyStdDevYears` (`:15,17`) | Início do séc. XIX: 15–16 (Tanner, citado); meados do séc. XIX: 16–17 (Ong et al. 2006); **dp acima de 2,5 anos** na França de meados do séc. XIX, ~1 ano hoje (PubMed 20954458) | [B] | média **confere** (limite baixo); **dp baixo — sugerido ~1,6–2,0** (parte da dispersão histórica já vem do atraso pela fome, `UndernutritionPubertyDelayYears`) |
| Dismenorreia que limita: 15% | `DysmenorrheaShare` (`:76`) | Ju, Jones & Mishra 2014, *Epidemiol Rev* 36:104–113: prevalência 16–91%, dor intensa em 2–29% | [B] (abstract) | **confere** |
| TPM moderada ~20% | `PremenstrualShare` (`:82`) | Não conferido nesta rodada (lembrança: 20–30% moderada/intensa, ACOG) | [M] | **sem conferência** (manter) |
| Depressão pós-parto 12% | `PostpartumDepressionShare` (`:96`) | O'Hara & Swain 1996, *Int Rev Psychiatry*: 13% (59 estudos); meta-análise de 2018: 17% em mães sem depressão prévia | [B] | **confere** |
| Janela fértil de 6 dias | ovulação + vida do espermatozoide | Wilcox, Weinberg & Baird 1995, *NEJM* | [M] | **sem conferência** (fonte clássica, não aberta) |
| Concepção pré-nupcial 20–35% | resultado | ver 123a | [L] | **confere** |
| Portador de recessiva ~1/25 | cenário de medida (L0 3,6%, L1 8%) | Fibrose cística na Europa: ~1 em 25 | [M] | **sem conferência** (só serve de referência de teste) |

## Demografia restante (item 19)

Dos três valores sem fonte do relatório de demografia: **razão de fecundidade 40–44 / 20–24**: n(a) de Coale & Trussell
(1974) lembrado como 0,460 / 0,431 / 0,396 / 0,321 / 0,167 / 0,024 → 0,36 [M]; buscas não acharam a tabela (Osier, MetricGate
sem números). **Morte neonatal de 12% aos 32–36 semanas** e **fatores de risco obstétrico** abaixo de 15, depois de 35, no 1º
parto e com paridade alta: nada novo achado. Ficam abertos, junto com a leitura dos originais [B] já listados.

Fontes: [Larmuseau 2019 (GenomeWeb)](https://www.genomeweb.com/node/32221) · [Alter, Newton & Oeppen 2020](https://pmc.ncbi.nlm.nih.gov/articles/PMC10923557) ·
[Campop 2024](https://www.campop.geog.cam.ac.uk/blog/2024/10/03/sex-before-marriage/) · [Western European marriage pattern](https://en.wikipedia.org/wiki/Western_European_marriage_pattern) ·
[Saleem 2014](https://scielosp.org/article/bwho/2014.v92n8/605-612) · [Ju 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4205951) ·
[O'Hara & Swain 1996](https://bohrium.dp.tech/paper/arxiv/811957504362676225) · [dp da menarca](https://www.pubmed.gov/20954458/) ·
[Ong 2006](https://jeremy-brown-vpk4.squarespace.com/s/secular-trends-on-onset-of-puberty-2006.pdf) · [separação por crueldade](https://www.historyworkshop.org.uk/womens-history/runaway-wives-in-medieval-london/)

**AD-164, item 6:** Knodel 1988 e Jaadla & Lust 2021 (Estônia) seguem sem número lido (PDF com fonte codificada). O resumo do
buscador para a Espanha rural 1750–1959 (Marco-Gracia, CPoS) dá ~40% dos que perderam a mãe no 1º ano chegando aos 15 [B]; sem
o braço com mãe da mesma fonte, a razão do modelo (0,39) não se confere. Fica aberto.
