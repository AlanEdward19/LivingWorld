# Conferência em fonte: lavoura, trabalho, negócio e crédito (AD-161)

Código lido em `perf/decision-scan-costs` (`a428cd11`). Graus **[L] [B] [C] [M]** como em
[conferencia-demografia](conferencia-demografia.md). Unidade de comida do código: uma ração de grão de ~700 g
(`InventionRules.UnitGrams[1]`, `Domain/Invention/InventionRules.cs:146`); conversões marcadas [C] usam essa equivalência.

## Lavoura e terra (itens 25, 35, 60) — `AgricultureRules` (`Domain/Economy/AgricultureRules.cs`)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 25a | ~4 grãos colhidos por semente | `YieldPerSeed` 4,0 (`:22`) | 14 solares do bispo de Winchester, 1283–1349: trigo ~4,0 por semente, ~515 kg/ha bruto e ~385 líquido; colheitas ruins de 50–100 kg/ha (Titow 1972 e Farmer 1977 na base) | [B] (resumo do buscador; o PDF era imagem) | **confere** |
| 25b | CV da safra 20% | variação anual do campo | Um solar de Winchester em 1409: CV de 13,2% com a mistura de grãos e **19,4% só com trigo**, sobre as 20 safras anteriores (blog de história econômica da LSE, 2023). Entre solares e anos, Winchester 1283–1349 dá CV de ~37–40% (resumo, OCR ruim) | [L] LSE; [B] Winchester | **confere** para um campo; a cauda de anos péssimos (50–100 kg/ha) não sai de um CV de 20% gaussiano |
| 25c | Perda no celeiro 15% ao ano | `BarnLossPerYear` 0,15 (`:30`) | Armazenagem tradicional moderna (Quênia, Índia, Tanzânia): ~6–15%, até 34% em paiol aberto; nada medieval achado | [B] | **confere** (alto da faixa) |
| 25d | 1.300 graus-dia até maturar | `DevelopmentToMaturity` (`:18`) | Trigo de inverno real ~1.800–2.200 °C·dia base 0 | [M] | **fora da faixa** por desenho (calibrado ao mapa, já declarado no AD-135); manter até o clima ser conferido |
| 25e | Ceifa de 100 por pessoa-dia | `ReapPerWorkerDay` (`:27`) | Foice: ½ a ⅔ de acre por dia (Holderness, década de 1840); "~0,3 acre" (página sem data). 0,12–0,27 ha/dia × ~515 kg/ha ≈ 60–140 kg; 100 rações ≈ 70 kg | [B]; [C] | **confere** (limite baixo) |
| 25f | Coelho: gestação 30 d, 60%/ano de mortalidade; lobo: uma ninhada por ano | regras de fauna do cenário (`EcologyScenarioSeeder`) | Biologia básica (coelho ~31 d; lobo reproduz uma vez por ano) | [M] | **sem conferência** (plausível) |
| 60a | 220 de semente por hectare (~135–154 kg/ha) | `SeedPerHectare` (`:37`) | Walter of Henley: trigo a 2–2½ bushels por acre (mínimo); 2 bu × ~27 kg / 0,405 ha ≈ 134 kg/ha | [B]; [C] | **confere** |
| 60b | 1,4 ha semeados por fundador | `SownHectaresPerFounder` (`:36`) | Inglaterra c.1300: ~10,7 milhões de acres de terra arável (Campbell, citado) para ~4,75 milhões de pessoas [M] ≈ 0,9 ha arável por pessoa, ~0,6 ha semeado com um terço em pousio [C] | [B] acres; [M] população | **fora da faixa para o pico de 1300 — sugerido ~0,7–1,0** (a regra de posse do AD-158 já usa 0,7 ha por pessoa); 1,4 só cabe numa fronteira de colonização ou depois de 1350 |
| 60c | Capina +25% de rendimento | `HandIntensityGain` (`:38`) | Nada achado | — | **sem fonte encontrada — escolha de modelo** |
| 60d | A mão que paga 320 por ano | `HandKeepPerYear` (`:39`) | 320 × 0,7 kg = 224 kg de grão ≈ 2.090 kcal/dia a ~3.400 kcal/kg; necessidade de adulto ~2.000–2.500 | [C] | **confere** |
| 35a | 160 vagas, 150 de semeadura por morador, dízimo 5%/mês com 12 folhas de reserva | AD-140 (`SeedPerResident` `:24`; `EconomyRules.SurplusLevyShare`) | Calibração para fechar o balanço de moeda | — | **sem fonte encontrada — escolha de modelo** |
| 35b | Faixas da doença (letalidade, incubação, sazonalidade dos 7 perfis) | `DiseasePresets.PreIndustrial` | **Não conferido nesta rodada** (tema próprio: precisa de epidemiologia histórica por doença) | — | fica aberto |

## Trabalho e refeição (itens 44, 53, 64)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 44a | 700 g por unidade de comida | `UnitGrams[1]` (`InventionRules.cs:146`) | Ver 60d: a ração do código fecha com ~2.100 kcal/dia | [C] | **confere** |
| 44b | Peixe 200 kg por célula d'água, 5%/mês de recomposição, 1 kg/h | `FishKgPerWaterCell`, `FishRegrowthPm`, `FishGramsPerHour` (`InventionRules.cs:157-161`) | Nada achado para água doce rasa de aldeia | — | **sem fonte encontrada — escolha de modelo** |
| 44c | Rumor 1,5× | AD-143 | — | — | **escolha de modelo** |
| 53 | Meio dia de despensa no turno; espera de 6 h dobrando até 48 h; limiar de trabalho 0,7 (marco "tools") | AD-148; `OntogenyDefaults.HumanRequirements[Work]` (`Domain/Ontogeny/OntogenyDefaults.cs:52`) | O marco "tools" (mediana 3 anos) é desenvolvimento infantil de uso de ferramenta simples [M]; os tempos de espera são de agenda | [M] | **sem fonte encontrada — escolha de modelo** |
| 64a | Refeição do empregador ao preço da venda | `Payroll.BoardCharge` | Estatuto de 1446: pedreiro livre 4 d por dia com comida e 5½ d sem (comida ≈ 27% do salário sem comida); vidraceiro elisabetano 9 d contra 13 d (~31%) | [B] (blog sobre o estatuto; tabela elisabetana) | **confere** em ordem de grandeza (a comida vale ~¼–⅓ do salário de ofício) |
| 64b | Desconto até o líquido inteiro | `EconomyRules.BoardMaxWageShare` 1,0 | No salário rural composto a parte em espécie era a **maior** (Claridge & Delabastita, 1270–1440, resumo) | [B] | **confere** para o lavrador; para ofício, o histórico dá ~¼–⅓ |

## Negócio (itens 68, 69, 70) — `VentureRules` (`Domain/Economy/VentureRules.cs`)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 68a | Mestre aos 21 | `MinAgeYears` (`:13`) | Aprendizagem obrigatória de 7 anos (Statute of Artificers 1563); fim aos 24 nas cidades [M] | [L] 7 anos; [M] 24 | **plausível** (aprendiz aos 12 + 7 = 19; o costume urbano pedia ~24) |
| 68b | Habilidade 30, chance 0,25/mês, 10% abaixo do concorrente, foro de 6 meses, obra de 60 dias-homem, 40 kg de ferro, giro de 6 meses, reserva 6 × (0,5 + aversão) | `VentureRules` (`:16-46`) | Nada achado | — | **sem fonte encontrada — escolha de modelo** |
| 69 | Confiança mínima 60, até 2 sócios | `MinPartnerTrust`, `MaxPartners` (`:50-51`) | *Commenda*: na forma bilateral o investidor põe ⅔ do capital e o viajante ⅓, lucro meio a meio; na unilateral o investidor põe tudo e leva ¾ do lucro; as *compagnie* familiares tinham vários sócios | [L] Wikipedia *Commenda* (secundária) | **escolha de modelo** (o teto de 2 é estreito para companhia, cabe numa oficina de aldeia) |
| 70 | Ferreiros por habitante (~1 por 200–400?) | resultado (~1 por vila) | Listas da poll tax de 1379/1381 mostram ferreiros na maioria das vilas (Howdenshire, Hinckford), sem razão por habitante | [B] | **não conferido** (fica aberto: contar ferreiros/pagantes em Fenwick, *Poll Taxes*) |

## Crédito (itens 90–96, 130–132) — `CreditRules` (`Domain/Economy/CreditRules.cs`)

| # | Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|---|
| 90 | Acréscimo 10%/ano, multa 5%, teto 20%; parente 0% | `AnnualCharge`, `LatePenaltyShare`, `MaxAnnualCharge` (`:12-20`) | Teto régio dos judeus ingleses: 2 d por libra por semana = **43,3%/ano** (1 d = 21,7%; 4 d = 86,7%) (Koyama 2010, *Const Polit Econ*); montes de piedade ~5% (4–15% nos primeiros; Prato 10% até 1493, depois 5%); penhoristas e judeus na Itália 20–30%; a proibição empurrava o juro para formas disfarçadas | [B] | **confere** (entre o monte de piedade e o penhorista; nenhum número de crédito entre aldeões achado) |
| 91 | Prazos 36 meses (abertura), 12 (giro, comida); calote após 6 meses; memória de 10 anos; falência após 6 meses | `FoundingTermMonths`, `WorkingTermMonths`, `FoodTermMonths`, `DefaultAfterMissedMonths`, `DefaultMemoryYears`, `BankruptcyMonths` (`:23-81`) | Briggs 2009, *Credit and Village Society*, p. 71: só 13 casos com data do crédito e do vencimento, **de um dia a um ano** | [B] (citado em resenha) | 12 meses **confere**; **36 meses fora da faixa — sugerido ≤ 12** (até 24 só com garantia em terra ou prédio); calote, memória e falência: **escolha de modelo** |
| 130 | Carência de 6 meses na abertura | `FoundingGraceMonths` (`:28`) | Dentro do prazo de até um ano de Briggs; nenhum caso de "carência" lido | [B] | **confere** (dentro da faixa) |
| 131 | Prazo da abertura até 60 meses | `MaxFoundingTermMonths` (`:32`) | idem: crédito de aldeia ≤ 1 ano | [B] | **fora da faixa — sugerido ≤ 24 com garantia** |
| 92 | Financia até metade do capital (× 1,5 − aversão); parcela até metade do ganho | `MaxLoanShare`, `MaxInstallmentShare` (`:41-44`) | Sem fonte para empréstimo; a *commenda* (sociedade, não dívida) financiava ⅔ a 100% do capital | [L] Wikipedia | **sem fonte encontrada — escolha de modelo** |
| 132 | Salário do fundador conta inteiro no ganho | `OwnWageInCapacityShare` 1,0 (`:37`) | — | — | **sem fonte encontrada — escolha de modelo** |
| 93 | Confiança 40, +40 parente, +40 garantia, má fama > 30 fecha | `MinLenderTrust`, `KinTrust`, `CollateralTrust`, `MaxInfamy` (`:48-53`) | — | — | **escolha de modelo** |
| 94 | Tesouro empresta até 25%; giro de 3 meses de folha; gatilho de 7 dias de comida; penhora deixa 1 mês | `CityLendShare`, `WorkingLoanPayrollMonths`, `FoodLoanPantryDays`, `DistraintExemptWageMonths` (`:65-76`) | — | — | **escolha de modelo** |
| 95 | Salário atrasado antes dos empréstimos com garantia | `Bankruptcy.PayArrears` primeiro (`Simulation/Economy/Credit/Bankruptcy.cs:34,74`) | O primeiro estatuto inglês de falência (34 & 35 Hen. VIII c. 4, 1543) trouxe a divisão *pari passu*, proporcional ao devido, só para mercadores; nada sobre preferência de salário (preferência de salário é do séc. XIX [M]) | [B] (Judiciary UK, Wikipedia *Statute of Bankrupts*) | **fora da faixa (anacrônico) — sugerido**: credor com penhor leva o bem penhorado; o resto em rateio proporcional entre todos, salário junto |
| 96 | Prédio tomado = foro + obra + equipamento, sem desconto | `Bankruptcy` | Venda forçada tem desconto [M]; nada medieval achado | — | **sem fonte encontrada — escolha de modelo** |

Fontes: [LSE 2023](https://blogs.lse.ac.uk/economichistory/2023/10/27/risk-management-in-the-14th-century) ·
[BAHS crop yields](https://www.bahs.org.uk/crop-yields-database/) · [perdas de armazenagem](https://www.wikipedia.org/wiki/Post-harvest_losses_(grains)) ·
[colheita à foice](https://digi.ub.uni-heidelberg.de/diglit/tools_tillage1976_1979/0051) · [Koyama 2010](https://mason.gmu.edu/~mkoyama2/About_files/Koyama10c.pdf) ·
[montes de piedade](https://monspietatis.org/en/montipieta/index/6.html) · [Commenda](https://en.wikipedia.org/wiki/Commenda) ·
[Statute of Bankrupts](https://Www.wikipedia.org/wiki/Bankruptcy_Act_1542) · [Briggs 2009 (resenha)](https://reviews.history.ac.uk/review/835/print) ·
[salários com comida](https://forthright.space/2020/06/25/middle-class-masons-tumultuous-times/) · [Statute of Artificers](https://en.wikipedia.org/wiki/Statute_of_Artificers_1562)

**AD-164, item 70:** nenhuma razão ferreiros/habitantes achada (Powell 1896 e Fenwick listam pagantes da poll tax por vila, sem
a conta pronta). Fica aberto: contar à mão numa lista de Suffolk de 1381.
