# Conferência em fonte: aprendizado, emoção, cérebro e poderes por genética (AD-161)

Código lido em `perf/decision-scan-costs` (`a428cd11`). Graus **[L] [B] [C] [M]** como em
[conferencia-demografia](conferencia-demografia.md). Itens 15, 21, 33 de [08](../08-a-investigar.md) e 100–105 de
[08b](../08b-a-investigar.md). Os hormônios (item 29) estão em [conferencia-familia-e-nupcialidade](conferencia-familia-e-nupcialidade.md).

## Aprendizado de ofício e criação (item 15) — `SkillLearningRules` (`Domain/Population/Skills/SkillLearningRules.cs`)

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| Aptidão 0 até 5 anos, 0,4 aos 12, 1,0 dos 16 aos 30, 0,6 aos 60 | `AptitudeByAge` (`:29`) | Nenhuma curva de "taxa de aprendizado de ofício por idade" achada | — | **sem fonte encontrada — escolha de modelo** |
| A criança chega a até 50% do que a casa sabe | `HouseholdCeilingFraction` (`:30`) | — | — | **escolha de modelo** |
| Perda por desuso 4% ao ano | `DisuseLossPerYear` (`:32`) | Arthur, Bennett, Stanush & McNelly 1998, *Human Performance* 11:57–101 (53 artigos, 189 efeitos): perda de d ≈ −1,4 depois de mais de um ano sem uso, menor em tarefa física e natural; meta-análise de 2025 (*Psychol Bull*, 457 relatórios): metade do ganho de treino perdido em ~6,5 meses (precisão) a ~13 meses (velocidade) | [B] | **fora da faixa para habilidade recém-treinada, sem fonte para ofício de décadas**: os estudos medem treino curto em laboratório; o ofício sobreaprendido decai bem mais devagar. Manter 4% como escolha de modelo |
| Peso da criação na personalidade 0,3 | `RearingRules` (AD-132) | Genética do comportamento: herdabilidade dos Cinco Grandes ~40–50%; o ambiente **compartilhado** (o que a criação dá igual aos irmãos) fica perto de **zero** (Plomin & Daniels 1987, *Behav Brain Sci*; correlação entre irmãos adotivos "perto de zero") | [B] | **fora da faixa — sugerido ≤ 0,1** (o que pesa é o ambiente não compartilhado: experiências próprias) |

## Emoção e memória (item 21) — `PsycheRules` (`Domain/Cognition/PsycheRules.cs`)

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| Luto: meia-vida 120 d | `GriefHalfLifeDays` (`:32`) | Maciejewski, Zhang, Block & Prigerson 2007, *JAMA* 297:716 (233 enlutados): saudade no pico aos 4 meses, raiva aos 5, depressão aos 6; **todos os indicadores em queda por volta de 6 meses**; mais que isso pede avaliação | [B] (abstract e notas de imprensa) | **confere** em ordem de grandeza (o modelo começa no pico; a fonte sobe até 4–6 meses e cai) |
| Medo 21 d, raiva 30, alegria 14, vergonha 45, solidão 10 | `FearHalfLifeDays`…`LonelinessHalfLifeDays` (`:33-37`) | Verduyn & Lavrijsen 2015, *Motivation and Emotion* 39:119 (27 emoções): tristeza dura mais (~120 h), ódio ~60 h, alegria ~35 h; **vergonha, medo e irritação estão entre as mais curtas** (~meia hora a vergonha). São episódios, não humor de semanas | [B] | **sem fonte para as meias-vidas de semanas — escolha de modelo**; a ordem conflita só na vergonha (o código a faz a mais longa porque ela é estigma social, não episódio: manter, declarando) |
| Memória: meia-vida 365 d; 12 lembranças | `MemoryHalfLifeDays`, `MemoryCapacity` (`:16-19`) | — | — | **escolha de modelo** |

## Cérebro (item 33) — `BrainRules` (`Domain/Cognition/BrainRules.cs`)

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| Taxa 0,1 por ponto; plena até 20 anos, 0,25 aos 60; ruído de segregação ±100‰; puxão 1% da criação | `Default` (`:23-26`) | Ganho sináptico em milésimos é construção do modelo (ADR-0046); não há grandeza empírica correspondente | — | **sem fonte encontrada — é escolha de modelo**. A herança lamarckiana também é do desenho; ela contradiz o achado acima (ambiente compartilhado ~0), por isso o puxão de 1% deve ficar pequeno |

## Poderes por genética (itens 100–105) — `PowerGenetics` (`Domain/Extraordinary/PowerInheritanceRules.cs:22-37`)

| # | Número | Fonte | Grau | Veredito |
|---|---|---|---|---|
| 100 | Penetrância 0,9; expressividade dp 0,15 (+0,25 homozigoto, +0,25 por ponto acima do limiar) | Penetrância de doenças mendelianas vai de parcial a completa (retinoblastoma ~0,9, Huntington ~1) | [M] | **confere** (dentro da faixa real); expressividade: **escolha de modelo** |
| 101 | Início N(13; 1,5) anos | A puberdade **do próprio modelo** é mais tarde: menarca 15,0 e gonadarca 15,5, dp 1,2 (`EndocrineRules.cs:15-17`); a menarca histórica era 15–17 | [B] (ver hormônios) | **fora da faixa do próprio mundo — sugerido**: atrelar o início ao `Npc.PubertyTick` (ou N(15; 1,5)) |
| 102 | Mutação 1e-5 por locus por gameta | Huntington ~1e-6, acondroplasia ~1e-5 (3–6 × 10⁻⁵ de incidência esporádica), NF1 ~5e-5 a 1e-4 por gameta; genes típicos 10⁻⁶ a 10⁻⁵ (Crow 2000; tabelas de Pierce, *Genetics*) | [B] | **confere** |
| 103 | Custo: fome e sede ×1,1, fertilidade ×0,9, vitalidade ×1,0 | O poder é fictício | — | **escolha de modelo** (calibrar pela seleção desejada) |
| 104 | Poligênico: 4 loci, q = 0,25, limiar 5 cópias, ambiente dp 0,5 | A forma (suscetibilidade com limiar, Falconer 1965) é o modelo padrão de traço poligênico [M]; os valores são do cenário | [M] | forma **confere**; valores **escolha de modelo** |
| 105 | Prevalência 8% (2% por poder) | Cenário de medida | — | **escolha de modelo** |

Também conferido de passagem: a doença recessiva de teste (`HeritableDisorderRules`, frequência do alelo 0,02, mutação 1e-5,
`Domain/Population/Body/HeritableDisorderRules.cs:21`) fica na faixa de 10⁻⁵ acima.

Fontes: [Arthur et al. 1998](https://Www.Gwern.net/doc/psychology/spaced-repetition/1998-arthur.pdf) · [meta-análise 2025](https://pubmed.ncbi.nlm.nih.gov/40455501/) ·
[Plomin & Daniels 1987](https://www.cambridge.org/core/services/aop-cambridge-core/content/view/S0140525X00055941) ·
[Maciejewski 2007](https://pubmed.ncbi.nlm.nih.gov/17312291/) · [Verduyn & Lavrijsen 2015](https://ppw.kuleuven.be/okp/_pdf/Verduyn2015WELLA.pdf) ·
[taxas de mutação](https://digfir-published.macmillanusa.com/pierce5e/asset/img_ch18/c18_tab03.html) · [Crow 2000](https://fohs.bgu.ac.il/develop/DB2/Where%20are%20the%20genes/Crow,%202000.pdf)
