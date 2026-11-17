# Conferência em fonte: invenção, materiais e força humana (AD-161)

Código lido em `perf/decision-scan-costs` (`a428cd11`). Graus **[L] [B] [C] [M]** como em
[conferencia-demografia](conferencia-demografia.md). Itens 31, 44 (carga) e 73–78 de [08](../08-a-investigar.md)/[08b](../08b-a-investigar.md).

## Materiais e água (item 31) — `InventionRules` (`Domain/Invention/InventionRules.cs`)

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| Madeira 600 kg/m³ | material 1 (`:218-219`) | *Wood Handbook* (USDA FPL): carvalho branco densidade básica ~0,68; pinho de folha longa ~0,59 | [B] (tabela derivada do Handbook num capítulo de compósitos) | **confere** (meio entre conífera e folhosa) |
| Madeira 40 MPa "de projeto" | idem | Módulo de ruptura da madeira limpa ~100 MPa (carvalho branco ~105, pinho ~100); a tração paralela tem poucos ensaios | [B] | **confere** como valor de projeto (~40% da madeira limpa, cobre nós e defeitos) |
| Junco 200 kg/m³, 5 MPa, 8 meses de vida | material 2 (`:220-221`) | Nada achado para feixe de *Phragmites* | — | **sem fonte encontrada — escolha de modelo** |
| Couro cru 950 kg/m³, 20 MPa | material 3 (`:222-223`) | Couro ~860–1.000 kg/m³ | [M] | **sem conferência** (plausível) |
| Pedra 2.600 kg/m³ | material 5 (`:226-227`) | Granito ~2.650–2.750, calcário 2.500–2.700 | [M] | **confere** em ordem de grandeza (sem página aberta) |
| Resina só revestimento | material 4 | — | — | **escolha de modelo** |
| 75 W de remada sustentada | `HumanPowerW` (`:25`) | Potência humana sustentada por horas ~75 W (Wilson, *Bicycling Science*) | [M] | **sem conferência** (valor clássico) |
| Coeficientes de arrasto, GM ≥ 30 mm, borda livre ≥ 80 mm | `MinMetacentricHeightMm`, `MinFreeboardMm` (`:47,50`) | Nada achado para canoa ou jangada de madeira | — | **sem fonte encontrada — escolha de modelo** |
| 4 células de vau por célula d'água; minutos de coleta por kg | `Mobility.TravelTicks`; receitas | — | — | **escolha de modelo** (já declarada no AD-141) |

## Carga a pé (itens 44, 78)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 44/78 | Uma pessoa leva 30 kg por viagem | `FootCarryKg` (`InventionRules.cs:142`) | Limpopo (África do Sul): recipiente de água na cabeça com média de **19,5 kg**; "mulheres levam tipicamente 20 kg" (UEA); Índia: recomendação de ~15 kg na cabeça, ~25 kg com dois potes como tarefa das mais cansativas | [B] (resumo de *Environmental Health* 2010 e da UEA) | **fora da faixa — sugerido 20 kg** (25 como teto de quem é forte) |

## Veículo de terra (itens 73–76) — `InventionRules.Land` (`Domain/Invention/InventionRules.Land.cs`)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 73 | Força humana sustentada puxando 250 N | `HumanPullN` (`:9`) | Snook & Ciriello 1991 (*Ergonomics* 34:1197) é a tabela de referência e a revisão de Garg et al. 2014 diz que segue válida, mas **nenhum valor em N foi lido** (revisão no PMC só com equações garbled; tabela da Liberty Mutual em 404). FAO: boi sustenta 10–12% do peso, equino 12–14%, sem número humano | [B] contexto | **não conferido** (fica aberto). Lembrança [M]: puxar por horas aceitável para a maioria dos homens fica em ~150–200 N; 250 N seria o alto |
| 74a | Arrastar no chão 0,55 | `DragFrictionPm` (`:12`) | Toras em trilha plana: coeficiente de arraste de ~0,32 com uma ponta suspensa; a tora inteira no chão pede ~2× a força (estudo florestal croata citando Sever 1990) | [B] | **confere** (~0,6 com a peça inteira no chão) |
| 74b | Patim 0,30, até 0,10 pelo acabamento | `SkidFrictionPm`, `SkidFinishGainPm` (`:16-17`) | Madeira sobre madeira seca 0,25–0,5; madeira encerada sobre neve molhada 0,14 / 0,10; sobre neve seca 0,04; couro sobre madeira 0,3–0,4 | [L] Engineering ToolBox | **confere** (0,10 só vale para neve; em terra o piso deveria ficar perto de 0,25) |
| 75a | Braço de rolamento 15 mm (Crr ≈ 0,05 com roda de 0,3 m) | `RollingResistanceMm` (`:23`) | Pneu em "solo médio duro, cascalho solto" 0,04–0,08; em areia solta 0,2–0,4 | [L] Engineering ToolBox | **confere** (roda de madeira estreita afunda mais: 0,05 é o lado otimista) |
| 75b | Eixo de madeira engraxado 0,15 | `AxleFrictionPm` (`:26`) | Madeira sobre madeira 0,25–0,5 seca; com graxa, menos | [L] idem | **sem fonte direta** (plausível) |
| 76a | Granel 800 kg/m³ | `CargoBulkKgM3` (`:30`) | Trigo ~750–800 kg/m³ (peso hectolítrico 75–80 kg/hl) | [M] | **confere** (sem página aberta) |
| 76b | Pilha de 0,5 m, patim 4× mais comprido que largo, tomba com contato < ½ da largura | `PileHeightMm`, `RunnerSlenderness` (`:20,29`) | — | — | **escolha de modelo** |

Fontes: [Engineering ToolBox, atrito](https://www.engineeringtoolbox.com/friction-coefficients-d_778.html) ·
[Engineering ToolBox, rolamento](https://www.engineeringtoolbox.com/rolling-friction-resistance-d_1303.html) ·
[arraste de toras](https://hrcak.srce.hr/file/6540) · [carga de água na cabeça](https://doi.org/10.1186%2F1476-069X-9-52) ·
[UEA, crippling burdens](https://www.uea.ac.uk/research/research-archive/crippling-burdens) · [Garg et al. 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4672999/) ·
[FAO, tração animal](https://www.fao.org/4/w0613t/w0613T0g.htm) · [Wood Handbook](https://research.fs.usda.gov/treesearch/37427)

**AD-164, item 73:** Snook & Ciriello 1991 segue sem tabela legível (os PDFs da Ohio BWC e da Liberty Mutual não abrem como
texto). Um guia da indústria automobilística dá 24 lb (~107 N) de empurrar/puxar sustentado com as duas mãos
([Ergoweb](https://ergoweb.com/force-guidelines/), [L], limite de projeto para quase todos). 250 N fica acima da faixa provável:
candidato a 150–200 N em [correções](correcoes-sugeridas.md) (#13), sem aplicar.
