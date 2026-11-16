# Conferência em fonte: crime contra a pessoa (AD-161)

Código lido em `perf/decision-scan-costs` (`a428cd11`), `Domain/Crime/PersonCrimeRules.cs`. Graus **[L] [B] [C] [M]** como
em [conferencia-demografia](conferencia-demografia.md). Itens 81–84 de [08b](../08b-a-investigar.md).

Ressalva comum: as taxas do código são **chance por ano de um agressor de propensão 1** (antes dos multiplicadores de
estresse, ciúme e histórico); as fontes dão **prevalência anual de vítimas**. Só a medição transforma uma na outra, por isso
o veredito diz "medir a prevalência" quando a ordem de grandeza não fecha à primeira vista.

## Violência doméstica (item 81) — `DomesticViolenceRules`

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| 0,012 por ano (propensão 1), multiplicada por estresse (fome 1,5, miséria 1,0, desemprego 0,5), ciúme 2,0, histórico 1,5 | `PerYear` (`:12`), `HungerStress`…`HistoryGain` (`:15-23`) | Sardinha et al. 2022, *Lancet*, a partir da base da OMS de 2018 (366 estudos, 161 países, 2 milhões de mulheres): **13%** (IC 10–16%) das mulheres de 15–49 anos com parceiro sofreram violência física ou sexual do parceiro **no último ano**; 27% na vida. Sul da Ásia 19%, África subsaariana central 32% | [B] (resumos do estudo e da OMS) | a referência lembrada (13%) **confere**; a taxa do código **parece baixa** (~1–5% efetivos por agressor) mas é outra grandeza: **medir a fração anual de esposas agredidas** e mirar ~10–20% (época que tolerava o "castigo") |
| Norma tolerava o "castigo moderado" | `ReportFactor` 0,15, `ConvictFactor` 0,5 (`:39-42`) | Hanawalt admite que a violência ocasional era "aceita e esperada"; homens eram citados nos tribunais de bairro de Londres por **não** corrigir a mulher "ralhadora" | [B] (History Workshop, resumo) | **confere** (direção) |
| Dano 0,6× o da rua; golpe mortal 0,4% | `HarmScale`, `LethalChance` (`:29-30`) | Nada achado com número | — | **sem fonte encontrada — escolha de modelo** |
| Homicídios dentro da família na Inglaterra do séc. XIII (~1/5?) | referência | Given 1977 (*Society and Homicide in Thirteenth-Century England*) trata de conflitos de família, senhor e criado e vizinhos, mas **nenhum número foi lido**; os números do livro diferem da errata (Continuity and Change); Stone 1983 e Sharpe 1981/1985 discutem o dado | [B] escopo | **não conferido** (lembrança [M]: Given dá fração **baixa**, ~8%, não 1/5) |
| Mágoa conjugal +20 por agressão | `SpouseGrievance` (`:33`) | — | — | **escolha de modelo** |

## Sequestro (item 82) — `KidnappingRules`

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| 0,002 por ano; resgate metade da moeda (teto 400); busca 2%/dia (4× com suspeita); fuga 3%/dia; prazo 21 d; morte no fim 5% (3× na vingança) | `PerYear` (`:54`), `RansomShare`, `RansomMaxCoin`, `SearchFindPerDay`, `SuspectSearchFactor`, `EscapePerDay`, `MaxHoldDays`, `KillAtEndChance`, `VengeanceKillFactor` (`:61-84`) | Nenhuma taxa medieval achada. *Raptus* nos rolos ingleses cobria do estupro ao rapto e à fuga consentida para casar (Legal History Miscellany, 2026; Dunn, *Stolen Women in Medieval England*, 2013, citado) | [B] | **sem fonte encontrada — escolha de modelo**; o rapto mais comum na fonte é **para casamento** (já em #164) |

## Violência sexual (item 83) — `SexualViolenceRules`

| Número | Código | Fonte | Grau | Veredito |
|---|---|---|---|---|
| Gravidez: fração 0,25 do ciclo por ato | `SingleActFecundability` (`:110`) | Holmes, Resnick, Kilpatrick & Best 1996, *Am J Obstet Gynecol*: **5,0% por estupro** entre vítimas de 12 a 45 anos (amostra nacional dos EUA; a contagem de casos é contestada, a taxa segue citada). 0,25 × fecundabilidade de pico 0,20 = 5% | [B] (abstract no PubMed); conta [C] | **confere** |
| Estupro ~1% das acusações de crime grave, quase sem condenação (Hanawalt 1979) | `ReportFactor` 0,3, `ConvictFactor` 0,5, forca 15%, banimento 40% (`:117-118`) | O livro (1300–1348) é a referência usual da raridade e da condenação baixa, mas **o número não foi lido**; a definição de *raptus* mistura casos | [B] escopo | **não conferido** (direção confere) |
| 0,004 por ano; mesmo sexo 0,1; revelação 0,3 por parente | `PerYear` (`:101`), `SameSexWeight` (`:107`), `DisclosureChance` (`:113`) | Nada achado | — | **sem fonte encontrada — escolha de modelo** |

## Separação pela violência (item 84)

Ver [conferencia-familia-e-nupcialidade](conferencia-familia-e-nupcialidade.md), linhas 84a e 84b: separação "de mesa e leito"
por *saevitia* existia, exigia prova de violência excessiva e era rara (Helmholz 1974); o efeito de −5% na natalidade não
tem fonte.

Fontes: [OMS 2018](https://www.who.int/publications/i/item/9789240026681) · [Sardinha 2022 (resumo)](https://www.scimex.org/newsfeed/one-in-four-women-whove-been-in-relationships-globally-have-experienced-domestic-violence) ·
[Holmes 1996](https://pubmed.ncbi.nlm.nih.gov/8765248/) · [raptus](https://legalhistorymiscellany.com/2026/05/08/reading-records-of-raptus-from-thirteenth-century-england/) ·
[Given 1977](https://krimdok.uni-tuebingen.de/Record/1608248275) · [History Workshop](https://www.historyworkshop.org.uk/womens-history/runaway-wives-in-medieval-london/)
